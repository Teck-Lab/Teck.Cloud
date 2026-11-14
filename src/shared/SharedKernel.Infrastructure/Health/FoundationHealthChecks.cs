using HealthChecks.ApplicationStatus.DependencyInjection;
using HealthChecks.RabbitMQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Npgsql;
using SharedKernel.Persistence.Caching;
using Wolverine.HealthChecks;
using Wolverine.Runtime;

namespace SharedKernel.Infrastructure.Health;

/// <summary>Mandatory service checks; database checks are selected by provider composition.</summary>
public static class FoundationHealthChecks
{
    internal static readonly string[] DependencyTags = ["ready", "dependencies"];

    /// <summary>Checks a PostgreSQL data source already owned by the service's DI container.</summary>
    /// <param name="services">Host services.</param>
    /// <param name="dataSource">The existing read, write or shared data source.</param>
    /// <param name="name">Unique endpoint name, such as postgres-read.</param>
    /// <returns>The registrations.</returns>
    public static IServiceCollection AddFoundationPostgresHealth(this IServiceCollection services, Func<IServiceProvider, NpgsqlDataSource> dataSource, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _ = services.AddHealthChecks().AddNpgSql(dataSource, name: name, tags: DependencyTags, timeout: TimeSpan.FromSeconds(5));
        return services;
    }

    internal static void Register(IHostApplicationBuilder builder)
    {
        var seconds = builder.Configuration.GetValue("Health:TimeoutSeconds", 5);
        var freeMegabytes = builder.Configuration.GetValue("Health:System:MinimumFreeMegabytes", 512);
        if (seconds is < 1 or > 30 || freeMegabytes < 1)
        {
            throw new InvalidOperationException("Health timeout must be 1-30 seconds and disk threshold must be positive.");
        }

        var timeout = TimeSpan.FromSeconds(seconds);
        var checks = builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddCheck<StartupHealthCheck>("startup", tags: ["startup", "ready"], timeout: timeout)
            .AddApplicationStatus(name: "application", tags: DependencyTags, timeout: timeout)
            .AddDiskStorageHealthCheck(options => options.AddDrive(builder.Configuration["Health:System:Drive"] ?? Path.GetPathRoot(builder.Environment.ContentRootPath)!, freeMegabytes), name: "system-disk", tags: DependencyTags, timeout: timeout);
        AddDeferred(checks, "redis", provider => new DeferredHealthCheck(_ => Task.FromResult<IHealthCheck>(new RedisReadinessCheck(provider.GetRequiredService<RedisConnection>()))), timeout);
        AddDeferred(checks, "rabbitmq", provider => new DeferredHealthCheck(async token => new RabbitMQHealthCheck(await provider.GetRequiredService<RabbitMqHealthConnection>().GetAsync(token).ConfigureAwait(false))), timeout);
        AddDeferred(checks, "wolverine-bus", provider => new DeferredHealthCheck(_ => Task.FromResult<IHealthCheck>(new WolverineBusHealthCheck(provider.GetRequiredService<IWolverineRuntime>()))), timeout);
        AddDeferred(checks, "wolverine-listeners", provider => new DeferredHealthCheck(_ => Task.FromResult<IHealthCheck>(new WolverineListenerHealthCheck(provider.GetRequiredService<IWolverineRuntime>()))), timeout);
        _ = checks.AddOpenIdConnectServer(provider => ResolveAuthority(provider, builder.Configuration), isDynamicOpenIdProvider: false, name: "keycloak", tags: DependencyTags, timeout: timeout);
        _ = builder.Services.AddHttpClient("keycloak", client => client.Timeout = timeout)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });
    }

    private static void AddDeferred(IHealthChecksBuilder builder, string name, Func<IServiceProvider, IHealthCheck> factory, TimeSpan timeout) =>
        builder.Add(new HealthCheckRegistration(name, factory, HealthStatus.Unhealthy, DependencyTags, timeout));

    private static Uri ResolveAuthority(IServiceProvider provider, IConfiguration configuration)
    {
        var authority = provider.GetService<KeycloakAuthority>()?.Uri.AbsoluteUri ?? configuration["Authentication:Authority"];
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException("Configure an HTTPS Keycloak realm authority through foundation authentication or Authentication:Authority.");
        }

#pragma warning disable S1075 // URI path separators must remain '/' on Windows as well as Linux.
        var normalized = new UriBuilder(uri) { Path = uri.AbsolutePath.TrimEnd('/') + '/' };
#pragma warning restore S1075
        return normalized.Uri;
    }
}
