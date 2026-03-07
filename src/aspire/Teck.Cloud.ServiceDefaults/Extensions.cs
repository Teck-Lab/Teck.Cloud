#nullable enable
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Infrastructure.Observability;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods to add common .NET Aspire services such as service discovery, resilience, health checks, and OpenTelemetry.
/// </summary>
// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    /// <summary>
    /// Adds common .NET Aspire services such as service discovery, resilience, health checks, and OpenTelemetry to the specified <see cref="IHostApplicationBuilder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to configure.</param>
    /// <returns>The configured <see cref="IHostApplicationBuilder"/>.</returns>
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.AddTeckCloudObservability();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
    {
        // Turn on resilience by default
        http.AddStandardResilienceHandler();

        // Turn on service discovery by default
        http.AddServiceDiscovery();
    });

        return builder;
    }

    /// <summary>
    /// Adds default health checks, including a liveness check, to the specified <see cref="IHostApplicationBuilder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to configure.</param>
    /// <returns>The configured <see cref="IHostApplicationBuilder"/>.</returns>
    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()

            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live", "ready"]);

        return builder;
    }

    /// <summary>
    /// Maps default health check endpoints to the specified <see cref="WebApplication"/>.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication"/> to configure.</param>
    /// <returns>The configured <see cref="WebApplication"/>.</returns>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        var healthChecks = app.MapGroup("");

        healthChecks
            .CacheOutput("HealthChecks")
            .WithRequestTimeout("HealthChecks");

        // All health checks must pass for app to be
        // considered ready to accept traffic after starting
        healthChecks.MapHealthChecks("/health", new()
        {
            Predicate = static remote => remote.Tags.Contains("ready"),
        }).AllowAnonymous();

        // Only health checks tagged with the "live" tag
        // must pass for app to be considered alive
        healthChecks.MapHealthChecks("/alive", new()
        {
            Predicate = static remote => remote.Tags.Contains("live"),
        }).AllowAnonymous();

        CancellationTokenRegistration applicationStartedRegistration = default;
        applicationStartedRegistration = app.Lifetime.ApplicationStarted.Register(() =>
        {
            applicationStartedRegistration.Dispose();

            using var scope = app.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("HealthChecks");
            var options = scope.ServiceProvider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

            var readinessChecks = options.Registrations
                .Where(registration => registration.Tags.Contains("ready"))
                .Select(registration => registration.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToArray();

            var livenessChecks = options.Registrations
                .Where(registration => registration.Tags.Contains("live"))
                .Select(registration => registration.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToArray();

            logger.LogInformation(
                "Registered readiness health checks ({Count}): {Checks}",
                readinessChecks.Length,
                readinessChecks.Length == 0 ? "none" : string.Join(", ", readinessChecks));

            logger.LogDebug(
                "Registered liveness health checks ({Count}): {Checks}",
                livenessChecks.Length,
                livenessChecks.Length == 0 ? "none" : string.Join(", ", livenessChecks));
        });

        return app;
    }

    /// <summary>
    /// Adds multi-tenant support with Keycloak nested organization claim parsing.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="customerApiUrl">Optional URL to the Customer API for tenant details.</param>
    /// <returns>The configured host application builder.</returns>
    public static IHostApplicationBuilder AddMultiTenantSupport(this IHostApplicationBuilder builder, Uri? customerApiUrl = null)
    {
        _ = customerApiUrl;

        // Configure multi-tenancy options
        builder.Services.AddTeckCloudMultiTenancy(options =>
        {
            // Configure to use the new nested organization claim format
            options.OrganizationClaimName = "organization";
            options.MultiTenantResolutionStrategy = SharedKernel.Infrastructure.MultiTenant.MultiTenantResolutionStrategy.FromRequest;
            options.UseCustomerApiTenantStore = false;
        });

        return builder;
    }
}
