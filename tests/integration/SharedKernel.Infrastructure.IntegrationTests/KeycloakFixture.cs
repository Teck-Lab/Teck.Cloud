using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Networks;
using SharedKernel.Infrastructure.Auth;
using Testcontainers.Keycloak;
using Testcontainers.PostgreSql;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Disposable identity provider using the Aspire realm plus test-only identities.</summary>
public sealed partial class KeycloakFixture : IAsyncLifetime
{
    private readonly string _adminPassword = RandomNumberGenerator.GetHexString(48);
    private readonly string _userPassword = RandomNumberGenerator.GetHexString(48);
    private readonly KeycloakContainer _container;
    private readonly PostgreSqlContainer _database;
    private readonly HttpClient _http = new(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>Initializes a new instance of the <see cref="KeycloakFixture"/> class.</summary>
    public KeycloakFixture()
        : this(imageOverride: null, environment: null)
    {
    }

    internal KeycloakFixture(string? imageOverride, IReadOnlyDictionary<string, string>? environment)
    {
        using var runtime = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Identity", "runtime.json")));
        var image = imageOverride ?? runtime.RootElement.GetProperty("image").GetString();
        var databasePassword = RandomNumberGenerator.GetHexString(48);
        _database = new PostgreSqlBuilder("postgres:18-alpine")
            .WithNetwork(Network)
            .WithNetworkAliases("identity-db")
            .WithDatabase("keycloak")
            .WithUsername("identity")
            .WithPassword(databasePassword)
            .Build();
        var keycloak = new KeycloakBuilder(image)
            .WithPassword(_adminPassword)
            .WithNetwork(Network)
            .WithEnvironment("KC_DB", "postgres")
            .WithEnvironment("KC_DB_URL", "jdbc:postgresql://identity-db:5432/keycloak")
            .WithEnvironment("KC_DB_USERNAME", "identity")
            .WithEnvironment("KC_DB_PASSWORD", databasePassword)
            .WithEnvironment("KC_HTTP_ENABLED", "true")
            .WithEnvironment("KC_HOSTNAME_STRICT", "false")
            .WithEnvironment("TECK_CUSTOMER_GATEWAY_SECRET", GatewaySecret)
            .WithEnvironment("TECK_TENANT_READER_SECRET", TenantReaderSecret)
            .WithEnvironment("TECK_ADMIN_GATEWAY_SECRET", RandomNumberGenerator.GetHexString(48))
            .WithRealm(Path.Combine(AppContext.BaseDirectory, "Identity", "Realms", "teck-local-realm.json"));
        if (runtime.RootElement.TryGetProperty("optimized", out var optimized) && optimized.GetBoolean())
        {
            keycloak = keycloak.WithCommand(new OverwriteEnumerable<string>(["start", "--optimized", "--import-realm"]));
        }

        if (environment is not null)
        {
            keycloak = keycloak.WithEnvironment(environment);
        }

        _container = keycloak.Build();
    }

    /// <summary>Gets the generated confidential gateway secret.</summary>
    public string GatewaySecret { get; } = RandomNumberGenerator.GetHexString(48);

    /// <summary>Gets the disposable realm token endpoint.</summary>
    public Uri TokenEndpoint => new(new Uri(_container.GetBaseAddress()), "realms/teck-local/protocol/openid-connect/token");

    /// <summary>Gets the test organization's generated identifier.</summary>
    public string OrganizationId { get; private set; } = string.Empty;

    internal string TenantReaderSecret { get; } = RandomNumberGenerator.GetHexString(48);

    internal Uri ServerAddress => new(_container.GetBaseAddress());

    internal INetwork Network { get; } = new NetworkBuilder().Build();

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        await Network.CreateAsync(deadline.Token).ConfigureAwait(false);
        await _database.StartAsync(deadline.Token).ConfigureAwait(false);
        await _container.StartAsync(deadline.Token).ConfigureAwait(false);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = "admin",
            ["password"] = _adminPassword,
        };
        using var admin = await GrantAsync(new Uri(new Uri(_container.GetBaseAddress()), "realms/master/protocol/openid-connect/token"), fields, deadline.Token).ConfigureAwait(false);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.RootElement.GetProperty("access_token").GetString());
        await ProvisionAsync(deadline.Token).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _container.DisposeAsync().ConfigureAwait(false);
        await _database.DisposeAsync().ConfigureAwait(false);
        await Network.DisposeAsync().ConfigureAwait(false);
    }

    internal async Task<System.Net.Http.Headers.AuthenticationHeaderValue> AdminAuthorizationAsync(CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = "admin",
            ["password"] = _adminPassword,
        };
        using var reply = await GrantAsync(new Uri(ServerAddress, "realms/master/protocol/openid-connect/token"), fields, cancellationToken).ConfigureAwait(false);
        return new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", reply.RootElement.GetProperty("access_token").GetString());
    }

    internal async Task<string> LoginAsync(string username, CancellationToken cancellationToken, string? scope = null)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "password",
            ["client_id"] = "integration-login",
            ["username"] = username,
            ["password"] = _userPassword,
        };
        if (scope is not null)
        {
            fields["scope"] = scope;
        }

        using var token = await GrantAsync(TokenEndpoint, fields, cancellationToken).ConfigureAwait(false);
        return token.RootElement.GetProperty("access_token").GetString()!;
    }

    internal OAuthClientOptions GatewayOptions(string? secret = null) => new()
    {
        TokenEndpoint = TokenEndpoint,
        ClientId = "customer-gateway",
        ClientSecret = secret ?? GatewaySecret,
        Transport = IdentityTransport.IsolatedDevelopment,
    };

    internal async Task<JsonDocument> UpdateOrganizationAsync(JsonObject changes, CancellationToken cancellationToken)
    {
        using var current = await AdminAsync(HttpMethod.Get, "organizations/" + OrganizationId, body: null, cancellationToken).ConfigureAwait(false);
        var representation = JsonNode.Parse(current.RootElement.GetRawText())!.AsObject();
        foreach (var change in changes)
        {
            representation[change.Key] = change.Value?.DeepClone();
        }

        return await AdminAsync(HttpMethod.Put, "organizations/" + OrganizationId, representation, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<JsonDocument> AdminAsync(HttpMethod method, string path, JsonNode? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(new Uri(_container.GetBaseAddress()), "admin/realms/teck-local/" + path));
        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonDocument.Parse(string.IsNullOrEmpty(text) ? "null" : text);
    }

    private async Task<JsonDocument> GrantAsync(Uri endpoint, Dictionary<string, string> fields, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new FormUrlEncodedContent(fields) };
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var disposeStream = stream.ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
