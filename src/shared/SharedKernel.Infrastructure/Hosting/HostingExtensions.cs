using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using Serilog;
using SharedKernel.Infrastructure.Health;
using SharedKernel.Infrastructure.MultiTenancy;
using SharedKernel.Persistence.Database;

namespace SharedKernel.Infrastructure.Hosting;

/// <summary>Host composition with mandatory health checks and explicitly configured dependencies.</summary>
public static class HostingExtensions
{
    /// <summary>Adds baseline host diagnostics and scoped operation services.</summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="exportTelemetry">False disables export; null enables export when an OTLP endpoint is configured.</param>
    public static void AddFoundationHost(this IHostApplicationBuilder builder, bool? exportTelemetry = null)
    {
        _ = builder.Services.AddPersistenceFoundation();
        _ = builder.Services.AddProblemDetails();
        FoundationHealthChecks.Register(builder);
        builder.AddFoundationTelemetry(exportTelemetry);
    }

    /// <summary>Registers generated endpoint types and OpenAPI generation.</summary>
    /// <param name="services">Host services.</param>
    /// <param name="discoveredTypes">FastEndpoints.Generator lists from the service assemblies.</param>
    /// <returns>The configured registrations.</returns>
    public static IServiceCollection AddFoundationApi(this IServiceCollection services, params List<Type>[] discoveredTypes)
    {
        _ = services.AddFastEndpoints(discoveredTypes);
        _ = services.SwaggerDocument();
        return services;
    }

    /// <summary>Configures JWT authority/audience validation, with no development bypass.</summary>
    /// <param name="services">Host services.</param>
    /// <param name="authority">The trusted HTTPS issuer.</param>
    /// <param name="audience">The service audience.</param>
    /// <returns>The configured registrations.</returns>
    /// <exception cref="ArgumentException">Authority is not HTTPS.</exception>
    public static IServiceCollection AddFoundationAuthentication(this IServiceCollection services, Uri authority, string audience)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        if (!string.Equals(authority.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            throw new ArgumentException("Token authority must use HTTPS.", nameof(authority));
        }

        _ = services.AddSingleton(new KeycloakAuthority(authority));
        _ = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.Authority = authority.AbsoluteUri;
            options.Audience = audience;
            options.MapInboundClaims = false;
            options.RequireHttpsMetadata = true;
        });
        _ = services.AddAuthorization();
        return services;
    }

    /// <summary>Maps local lifecycle probes, mandatory readiness and sanitized dependency diagnostics.</summary>
    /// <param name="app">The web app.</param>
    public static void MapFoundationHealth(this WebApplication app)
    {
        foreach (var tag in new[] { "live", "startup", "ready", "dependencies" })
        {
            var options = new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains(tag),
                ResultStatusCodes =
                {
                    [HealthStatus.Healthy] = StatusCodes.Status200OK,
                    [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
                    [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
                },
            };
            if (string.Equals(tag, "dependencies", StringComparison.Ordinal))
            {
                options.ResponseWriter = HealthReportWriter.WriteAsync;
            }

            _ = app.MapHealthChecks("/health/" + tag, options).ExcludeFromDescription();
        }
    }

    /// <summary>Installs operation scope after routing/authentication/authorization, then standard endpoints.</summary>
    /// <param name="app">The web app with authentication and authorization already configured.</param>
    /// <param name="exposeDocumentation">Explicitly expose OpenAPI and Scalar; secure at ingress when enabled.</param>
    public static void UseFoundationApi(this WebApplication app, bool exposeDocumentation = false)
    {
        _ = app.UseSerilogRequestLogging();
        _ = app.UseMiddleware<OperationScopeMiddleware>();
        if (app.Services.GetRequiredService<IServiceProviderIsService>().IsService(typeof(IOutputCacheStore)))
        {
            _ = app.UseOutputCache();
        }

        _ = app.UseFastEndpoints(config => config.Errors.UseProblemDetails());
        if (exposeDocumentation)
        {
            _ = app.UseOpenApi(options => options.Path = "/openapi/{documentName}.json");
            _ = app.MapScalarApiReference();
        }
    }
}
