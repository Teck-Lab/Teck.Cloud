#nullable enable
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;
using SharedKernel.Infrastructure.MultiTenant;

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
        builder.ConfigureOpenTelemetry();

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
    /// Configures OpenTelemetry logging, metrics, and tracing for the specified <see cref="IHostApplicationBuilder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to configure.</param>
    /// <returns>The configured <see cref="IHostApplicationBuilder"/>.</returns>
    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: builder.Environment.ApplicationName,
                serviceVersion: typeof(Extensions).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                serviceInstanceId: Environment.MachineName);

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddFusionCacheInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddMeter($"Wolverine:{builder.Environment.ApplicationName}")
                    .AddKeycloakAuthServicesInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()

                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    // .AddGrpcClientInstrumentation()
                     .SetResourceBuilder(resourceBuilder)
                    .AddHttpClientInstrumentation()
                    .AddFusionCacheInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddRedisInstrumentation()
                    .AddSource("Wolverine")
                    .AddKeycloakAuthServicesInstrumentation();
            });

        builder.AddOpenTelemetryExporters();
        builder.ConfigureSerilogWithOpenTelemetry();
        return builder;
    }

    private static IHostApplicationBuilder ConfigureSerilogWithOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ApplicationName", builder.Environment.ApplicationName)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                );

            // Check if OTLP endpoint is configured (Aspire will set this automatically)
            var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                loggerConfiguration.WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = otlpEndpoint;
                    options.Protocol = OtlpProtocol.Grpc;
                    options.IncludedData = IncludedData.TraceIdField |
                                          IncludedData.SpanIdField |
                                          IncludedData.MessageTemplateTextAttribute |
                                          IncludedData.SpecRequiredResourceAttributes;
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = builder.Environment.ApplicationName,
                        ["service.version"] = typeof(Extensions).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                        ["deployment.environment"] = builder.Environment.EnvironmentName
                    };
                });
            }

            if (builder.Environment.IsDevelopment())
            {
                loggerConfiguration.MinimumLevel.Debug();
            }
            else
            {
                loggerConfiguration.MinimumLevel.Information();
            }

            loggerConfiguration.MinimumLevel.Override("Microsoft", LogEventLevel.Warning);
            loggerConfiguration.MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information);
            loggerConfiguration.MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning);
        });

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.Configure<OpenTelemetryLoggerOptions>(logging => logging.AddOtlpExporter());
            builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddOtlpExporter());
            builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddOtlpExporter());
        }

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
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

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
        healthChecks.MapHealthChecks("/health").AllowAnonymous();

        // Only health checks tagged with the "live" tag
        // must pass for app to be considered alive
        healthChecks.MapHealthChecks("/alive", new()
        {
            Predicate = static remote => remote.Tags.Contains("live"),
        }).AllowAnonymous();

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
        // Configure multi-tenancy options
        builder.Services.AddTeckCloudMultiTenancy(options =>
        {
            // Configure to use the new nested organization claim format
            options.OrganizationClaimName = "organization";
            options.MultiTenantResolutionStrategy = SharedKernel.Infrastructure.MultiTenant.MultiTenantResolutionStrategy.Primary;

            // Enable or disable customer API store based on whether a URL is provided
            options.UseCustomerApiTenantStore = customerApiUrl != null;

            if (customerApiUrl != null)
            {
                // Add HTTP client for tenant API
                builder.Services.AddTenantHttpClient(customerApiUrl, "CustomerApi");

                // Configure the API endpoints
                options.CustomerApiOptions.HttpClientName = "CustomerApi";
                options.CustomerApiOptions.AllTenantsEndpoint = "api/tenants";
                options.CustomerApiOptions.TenantDetailsEndpoint = "api/tenants/{tenantId}";
                options.CustomerApiOptions.TenantByIdEndpoint = "api/tenants/id/{id}";
                options.CustomerApiOptions.TenantByNameEndpoint = "api/tenants/name/{name}";
            }
        });

        return builder;
    }
}
