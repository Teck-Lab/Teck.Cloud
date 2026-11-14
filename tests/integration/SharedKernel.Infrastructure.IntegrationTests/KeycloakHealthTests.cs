using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using SharedKernel.Infrastructure.Hosting;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Exercises the published OIDC discovery check without external identity credentials.</summary>
[Collection(TelemetryGroup.Name)]
public sealed class KeycloakHealthTests
{
    /// <summary>Realm-relative discovery, invalid responses, outages and recovery affect health.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task DiscoveryPreservesRealmAndRecovers()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddFoundationHost(exportTelemetry: false);
        var state = new DiscoveryState();
        Configure(builder, state);
        using var host = builder.Build();
        var checks = host.Services.GetRequiredService<HealthCheckService>();
        foreach (var (code, expected) in new[]
        {
            (HttpStatusCode.OK, HealthStatus.Healthy),
            (HttpStatusCode.ServiceUnavailable, HealthStatus.Unhealthy),
            (HttpStatusCode.OK, HealthStatus.Healthy),
        })
        {
            state.Status = code;
            var report = await checks.CheckHealthAsync(registration => string.Equals(registration.Name, "keycloak", StringComparison.Ordinal), TestContext.Current.CancellationToken).ConfigureAwait(true);
            report.Status.ShouldBe(expected);
            state.RequestUri.ShouldBe(new Uri("https://identity.test/realms/teck/.well-known/openid-configuration"));
        }

        state.Body = "{}";
        var invalid = await checks.CheckHealthAsync(registration => string.Equals(registration.Name, "keycloak", StringComparison.Ordinal), TestContext.Current.CancellationToken).ConfigureAwait(true);
        invalid.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    internal static void Configure(IHostApplicationBuilder builder, DiscoveryState state)
    {
        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Authentication:Authority"] = "https://identity.test/realms/teck" });
        _ = builder.Services.AddHttpClient("keycloak").ConfigurePrimaryHttpMessageHandler(() => new DiscoveryHandler(state));
    }

    internal sealed class DiscoveryState
    {
        internal HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        internal Uri? RequestUri { get; set; }

        internal string Body { get; set; } = """
            {"issuer":"https://identity.test/realms/teck","authorization_endpoint":"https://identity.test/realms/teck/protocol/openid-connect/auth","jwks_uri":"https://identity.test/realms/teck/protocol/openid-connect/certs","subject_types_supported":["public"],"id_token_signing_alg_values_supported":["RS256"]}
            """;
    }

    private sealed class DiscoveryHandler(DiscoveryState state) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            state.RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(state.Status) { Content = new StringContent(state.Body) });
        }
    }
}
