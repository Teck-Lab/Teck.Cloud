using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharedKernel.Api.TestHost;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Verifies health endpoints distinguish local lifecycle from required dependencies.</summary>
public sealed class HealthEndpointTests
{
    /// <summary>Missing dependencies cannot yield a successful readiness response.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task MissingDependenciesFailReadinessButNotLiveness()
    {
        await using var factory = new WebApplicationFactory<ProbeEndpoint>();
        using var client = factory.CreateClient();
        var cancellation = TestContext.Current.CancellationToken;
        foreach (var endpoint in new[] { "/health/live", "/health/startup" })
        {
            using var response = await client.GetAsync(new Uri(endpoint, UriKind.Relative), cancellation).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), cancellation).ConfigureAwait(true);
        ready.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        using var dependencies = await client.GetAsync(new Uri("/health/dependencies", UriKind.Relative), cancellation).ConfigureAwait(true);
        dependencies.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        using var report = JsonDocument.Parse(await dependencies.Content.ReadAsStringAsync(cancellation).ConfigureAwait(true));
        var checks = report.RootElement.GetProperty("checks");
        foreach (var name in new[] { "redis", "rabbitmq", "keycloak", "wolverine-bus", "wolverine-listeners" })
        {
            checks.GetProperty(name).GetProperty("status").GetString().ShouldBe("Unhealthy");
        }

        checks.TryGetProperty("system-disk", out _).ShouldBeTrue();
        checks.TryGetProperty("application", out _).ShouldBeTrue();
        report.RootElement.GetRawText().ShouldNotContain("exception", Case.Insensitive);
    }

    /// <summary>Degraded is not ready, and exception/data details never appear in diagnostics.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task DegradedChecksReturn503WithoutExposingDetails()
    {
        await using var original = new WebApplicationFactory<ProbeEndpoint>();
        await using var factory = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            _ = services.PostConfigure<HealthCheckServiceOptions>(options => options.Registrations.Clear());
            _ = services.PostConfigure<HealthCheckServiceOptions>(options => options.Registrations.Add(new HealthCheckRegistration("test-degraded", _ => new DegradedCheck(), failureStatus: null, ["ready", "dependencies"])));
        }));
        using var client = factory.CreateClient();
        foreach (var endpoint in new[] { "/health/ready", "/health/dependencies" })
        {
            using var response = await client.GetAsync(new Uri(endpoint, UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            body.ShouldContain("Degraded", Case.Sensitive);
            body.ShouldNotContain("private-marker", Case.Sensitive);
        }
    }

    private sealed class DegradedCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Degraded("private-marker", new InvalidOperationException("private-marker"), new Dictionary<string, object>(StringComparer.Ordinal) { ["secret"] = "private-marker" }));
    }
}
