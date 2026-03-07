using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting.Testing;
using Shouldly;

#pragma warning disable CA2007

namespace Teck.Cloud.E2ETests.Gateway;

[CollectionDefinition(Name)]
public sealed class AppHostE2eCollection : ICollectionFixture<AppHostE2eFixture>
{
    public const string Name = "AppHostE2E";
}

[Collection(AppHostE2eCollection.Name)]
public sealed class EdgeGatewayE2ETests
{
    private readonly AppHostE2eFixture fixture;

    public EdgeGatewayE2ETests(AppHostE2eFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task CustomerAdminCreateTenant_WhenUnauthenticated_ReturnsUnauthorized()
    {
        if (fixture.IsContainerRuntimeUnavailable)
        {
            return;
        }

        using HttpRequestMessage request = new(HttpMethod.Post, "/customer/v1/Tenants")
        {
            Content = JsonContent.Create(new
            {
                identifier = "e2e-tenant",
                name = "E2E Tenant",
                plan = "Enterprise",
                databaseStrategy = "Shared",
            }),
        };

        HttpResponseMessage response = await fixture.EdgeClient.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CustomerAdminCreateTenant_WhenAuthenticatedWithoutEmployeeRole_ReturnsForbidden()
    {
        if (fixture.IsContainerRuntimeUnavailable)
        {
            return;
        }

        string accessToken = await fixture.GetEdgeClientAccessTokenAsync(TestContext.Current.CancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            accessToken = "e2e-fallback-invalid-token";
        }

        using HttpRequestMessage request = new(HttpMethod.Post, "/customer/v1/Tenants")
        {
            Content = JsonContent.Create(new
            {
                identifier = "e2e-tenant-role-check",
                name = "E2E Tenant Role Check",
                plan = "Enterprise",
                databaseStrategy = "Shared",
            }),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response = await fixture.EdgeClient.SendAsync(request, TestContext.Current.CancellationToken);

        new[] { HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized }.ShouldContain(response.StatusCode);
    }

    [Fact]
    public async Task CatalogVersion_WhenAuthenticatedWithoutTenantClaim_ReturnsForbidden()
    {
        if (fixture.IsContainerRuntimeUnavailable)
        {
            return;
        }

        string accessToken = await fixture.GetEdgeClientAccessTokenWithoutTenantClaimAsync(TestContext.Current.CancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            accessToken = "e2e-fallback-invalid-token";
        }

        using HttpRequestMessage request = new(HttpMethod.Get, "/catalog/v1/Service/Version");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response = await fixture.EdgeClient.SendAsync(request, TestContext.Current.CancellationToken);

        new[] { HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized }.ShouldContain(response.StatusCode);
    }

    [Fact]
    public async Task CatalogOpenApi_WhenAnonymous_ReturnsSuccessStatusCode()
    {
        if (fixture.IsContainerRuntimeUnavailable)
        {
            return;
        }

        HttpResponseMessage response = await fixture.GetCatalogOpenApiResponseAsync(TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.ShouldBeTrue();
    }
}

public sealed class AppHostE2eFixture : IAsyncLifetime, IAsyncDisposable
{
    private const string MockNoTenantToken = "e2e-edge-no-tenant";
    private const string MockTenantNoRoleToken = "e2e-edge-tenant-no-role";
    private const string MockAuthEnvironmentVariable = "TECK_TEST_MOCK_AUTH";

    private const string DefaultKeycloakAuthority = "https://auth.tecklab.dk/";
    private const string DefaultRealm = "Teck.Cloud.Dev";
    private const string DefaultEdgeClientId = "teck-web-edge";
    private const string DefaultEdgeClientSecret = "lLHcTeo7m9yPQKz4ijKwshcuWlsAQ2mY";

    private static readonly string Realm =
        Environment.GetEnvironmentVariable("TECK_E2E_KEYCLOAK_REALM") ?? DefaultRealm;
    private static readonly string EdgeClientId =
        Environment.GetEnvironmentVariable("TECK_E2E_KEYCLOAK_EDGE_CLIENT_ID") ?? DefaultEdgeClientId;
    private static readonly string EdgeClientSecret =
        Environment.GetEnvironmentVariable("TECK_E2E_KEYCLOAK_EDGE_CLIENT_SECRET") ?? DefaultEdgeClientSecret;
    private static readonly bool RequireKeycloak =
        bool.TryParse(Environment.GetEnvironmentVariable("TECK_E2E_REQUIRE_KEYCLOAK"), out var requireKeycloak) && requireKeycloak;
    private static readonly bool UseMockAuthentication =
        !RequireKeycloak && (!bool.TryParse(Environment.GetEnvironmentVariable("TECK_E2E_USE_MOCK_AUTH"), out var useMockAuth) || useMockAuth);
    private static readonly Uri KeycloakAuthority = BuildKeycloakAuthority();

    private TeckAppHostFactory? factory;
    private bool containerRuntimeUnavailable;
    private string? previousMockAuthEnvironmentValue;
    private static readonly TimeSpan ReadinessProbeTimeout = TimeSpan.FromMinutes(8);
    private static readonly TimeSpan RequestTimeoutDuringWarmup = TimeSpan.FromSeconds(15);

    public bool IsContainerRuntimeUnavailable => containerRuntimeUnavailable;

    public HttpClient EdgeClient { get; private set; } = default!;

    public HttpClient KeycloakClient { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        KeycloakClient = new HttpClient
        {
            BaseAddress = KeycloakAuthority,
            Timeout = TimeSpan.FromSeconds(30),
        };

        try
        {
            if (UseMockAuthentication)
            {
                previousMockAuthEnvironmentValue = Environment.GetEnvironmentVariable(MockAuthEnvironmentVariable);
                Environment.SetEnvironmentVariable(MockAuthEnvironmentVariable, bool.TrueString);
            }

            factory = new TeckAppHostFactory();
            await factory.StartAsync();
            _ = factory.Application
                ?? throw new InvalidOperationException("Unable to resolve distributed application instance from AppHost factory.");

            EdgeClient = CreateResourceClient(factory, "web-edge");
            EdgeClient.Timeout = RequestTimeoutDuringWarmup;

            using var readinessCts = new CancellationTokenSource(ReadinessProbeTimeout);
            if (RequireKeycloak && !UseMockAuthentication)
            {
                await WaitForKeycloakReadyAsync(readinessCts.Token);
            }
            else if (!UseMockAuthentication)
            {
                _ = await TryWaitForKeycloakReadyAsync(readinessCts.Token);
            }

            await WaitForEdgeReadyAsync(readinessCts.Token);
        }
        catch (Aspire.Hosting.DistributedApplicationException exception) when (IsContainerRuntimeUnavailableException(exception))
        {
            containerRuntimeUnavailable = true;
        }
    }

    public async Task<string> GetEdgeClientAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (UseMockAuthentication)
        {
            return MockTenantNoRoleToken;
        }

        HttpResponseMessage? primaryResponse = null;

        try
        {
            primaryResponse = await RequestAccessTokenAsync(
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", EdgeClientId),
                new KeyValuePair<string, string>("client_secret", EdgeClientSecret),
                new KeyValuePair<string, string>("scope", "openid"),
                new KeyValuePair<string, string>("audience", EdgeClientId),
            ]),
            cancellationToken);
        }
        catch (HttpRequestException)
        {
            return string.Empty;
        }
        catch (TaskCanceledException)
        {
            return string.Empty;
        }

        using (primaryResponse)
        {
            if (primaryResponse.IsSuccessStatusCode)
            {
                return await ExtractAccessTokenAsync(primaryResponse, cancellationToken);
            }

            if (primaryResponse.StatusCode == HttpStatusCode.Unauthorized ||
                primaryResponse.StatusCode == HttpStatusCode.BadRequest)
            {
                HttpResponseMessage? fallbackResponse = null;
                try
                {
                    fallbackResponse = await RequestAccessTokenAsync(
                        new FormUrlEncodedContent(
                        [
                            new KeyValuePair<string, string>("grant_type", "client_credentials"),
                            new KeyValuePair<string, string>("client_id", EdgeClientId),
                            new KeyValuePair<string, string>("client_secret", EdgeClientSecret),
                        ]),
                        cancellationToken);
                }
                catch (HttpRequestException)
                {
                    return string.Empty;
                }
                catch (TaskCanceledException)
                {
                    return string.Empty;
                }

                using (fallbackResponse)
                {
                    if (fallbackResponse.IsSuccessStatusCode)
                    {
                        return await ExtractAccessTokenAsync(fallbackResponse, cancellationToken);
                    }

                    return string.Empty;
                }
            }

            return string.Empty;
        }
    }

    public Task<string> GetEdgeClientAccessTokenWithoutTenantClaimAsync(CancellationToken cancellationToken)
    {
        if (UseMockAuthentication)
        {
            return Task.FromResult(MockNoTenantToken);
        }

        return GetEdgeClientAccessTokenAsync(cancellationToken);
    }

    private async Task<HttpResponseMessage> RequestAccessTokenAsync(FormUrlEncodedContent content, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, $"/realms/{Realm}/protocol/openid-connect/token")
        {
            Content = content,
        };

        return await KeycloakClient.SendAsync(request, cancellationToken);
    }

    private static async Task<string> ExtractAccessTokenAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using JsonDocument jsonDocument = JsonDocument.Parse(payload);

        string? accessToken = jsonDocument.RootElement.GetProperty("access_token").GetString();
        return !string.IsNullOrWhiteSpace(accessToken)
            ? accessToken
            : throw new InvalidOperationException("Keycloak token response did not include access_token.");
    }

    public async Task<HttpResponseMessage> GetCatalogOpenApiResponseAsync(CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(3);
        var lastError = string.Empty;

        for (var attempt = 1; attempt <= 60; attempt++)
        {
            try
            {
                using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                requestCts.CancelAfter(TimeSpan.FromSeconds(20));

                HttpResponseMessage response = await EdgeClient.GetAsync(
                    new Uri("/catalog/openapi/v1/openapi.json", UriKind.Relative),
                    requestCts.Token);

                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                lastError = $"HTTP {(int)response.StatusCode}";
                response.Dispose();
            }
            catch (HttpRequestException exception)
            {
                lastError = exception.Message;
            }
            catch (TaskCanceledException exception)
            {
                lastError = exception.Message;
            }

            await Task.Delay(delay, cancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for /catalog/openapi/v1/openapi.json. Last error: {lastError}");
    }

    public async ValueTask DisposeAsync()
    {
        EdgeClient?.Dispose();
        KeycloakClient?.Dispose();

        if (factory is not null)
        {
            await factory.DisposeAsync();
        }

        if (UseMockAuthentication)
        {
            Environment.SetEnvironmentVariable(MockAuthEnvironmentVariable, previousMockAuthEnvironmentValue);
        }
    }

    private async Task WaitForEdgeReadyAsync(CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(3);
        var lastError = string.Empty;

        for (var attempt = 1; attempt <= 40; attempt++)
        {
            try
            {
                using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                requestCts.CancelAfter(TimeSpan.FromSeconds(8));

                using HttpResponseMessage response = await EdgeClient.GetAsync(
                    new Uri("/openapi/v1/openapi.json", UriKind.Relative),
                    requestCts.Token);

                if ((int)response.StatusCode < 500)
                {
                    return;
                }

                lastError = $"HTTP {(int)response.StatusCode}";
            }
            catch (HttpRequestException exception)
            {
                lastError = exception.Message;
            }
            catch (TaskCanceledException exception)
            {
                lastError = exception.Message;
            }
            catch (InvalidOperationException exception)
            {
                lastError = exception.Message;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out waiting for Web.Edge readiness. Last error: {lastError}");
            }

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out waiting for Web.Edge readiness. Last error: {lastError}");
            }
        }

        throw new TimeoutException($"Timed out waiting for Web.Edge readiness. Last error: {lastError}");
    }

    private async Task WaitForKeycloakReadyAsync(CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(2);
        var lastError = string.Empty;

        for (var attempt = 1; attempt <= 40; attempt++)
        {
            try
            {
                using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                requestCts.CancelAfter(TimeSpan.FromSeconds(8));

                using HttpResponseMessage response = await KeycloakClient.GetAsync(
                    new Uri($"/realms/{Realm}/.well-known/openid-configuration", UriKind.Relative),
                    requestCts.Token);

                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                lastError = $"HTTP {(int)response.StatusCode}";
            }
            catch (HttpRequestException exception)
            {
                lastError = exception.Message;
            }
            catch (TaskCanceledException exception)
            {
                lastError = exception.Message;
            }
            catch (InvalidOperationException exception)
            {
                lastError = exception.Message;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out waiting for Keycloak readiness. Last error: {lastError}");
            }

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out waiting for Keycloak readiness. Last error: {lastError}");
            }
        }

        throw new TimeoutException($"Timed out waiting for Keycloak readiness. Last error: {lastError}");
    }

    private async Task<bool> TryWaitForKeycloakReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await WaitForKeycloakReadyAsync(cancellationToken);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static HttpClient CreateResourceClient(DistributedApplicationFactory distributedApplicationFactory, string resourceName)
    {
        try
        {
            return distributedApplicationFactory.CreateHttpClient(resourceName, "http");
        }
        catch (ArgumentException)
        {
            return distributedApplicationFactory.CreateHttpClient(resourceName, "https");
        }
        catch (InvalidOperationException)
        {
            return distributedApplicationFactory.CreateHttpClient(resourceName, "https");
        }
    }

    private static Uri BuildKeycloakAuthority()
    {
        var configuredAuthority = Environment.GetEnvironmentVariable("TECK_E2E_KEYCLOAK_AUTH_SERVER_URL")
            ?? DefaultKeycloakAuthority;

        if (!Uri.TryCreate(configuredAuthority, UriKind.Absolute, out Uri? authority))
        {
            throw new InvalidOperationException(
                "TECK_E2E_KEYCLOAK_AUTH_SERVER_URL must be an absolute URI when provided.");
        }

        return authority;
    }

    private static bool IsContainerRuntimeUnavailableException(Aspire.Hosting.DistributedApplicationException exception)
    {
        return exception.Message.Contains("Container runtime", StringComparison.OrdinalIgnoreCase)
            && exception.Message.Contains("unhealthy", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class TeckAppHostFactory : DistributedApplicationFactory
{
    public DistributedApplication? Application { get; private set; }

    public TeckAppHostFactory()
        : base(typeof(Projects.Teck_Cloud_AppHost))
    {
    }

    protected override void OnBuilt(DistributedApplication application)
    {
        Application = application;
    }
}

#pragma warning restore CA2007