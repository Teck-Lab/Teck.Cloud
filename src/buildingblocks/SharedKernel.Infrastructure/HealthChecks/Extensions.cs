using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using SharedKernel.Core.Pricing;

namespace SharedKernel.Infrastructure.HealthChecks;

/// <summary>
/// Shared health check registration helpers for infrastructure dependencies.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Adds database health checks for write and optional read endpoints based on provider.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="provider">The selected database provider.</param>
    /// <param name="writeConnectionString">The write database connection string.</param>
    /// <returns>The same builder for chaining.</returns>
    public static WebApplicationBuilder AddReadWriteHealthChecks(
        this WebApplicationBuilder builder,
        DatabaseProvider provider,
        string writeConnectionString)
    {
        return AddReadWriteHealthChecks(builder, provider, writeConnectionString, null);
    }

    /// <summary>
    /// Adds database health checks for write and optional read endpoints based on provider.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="provider">The selected database provider.</param>
    /// <param name="writeConnectionString">The write database connection string.</param>
    /// <param name="readConnectionString">The optional read-only database connection string.</param>
    /// <returns>The same builder for chaining.</returns>
    public static WebApplicationBuilder AddReadWriteHealthChecks(
        this WebApplicationBuilder builder,
        DatabaseProvider provider,
        string writeConnectionString,
        string? readConnectionString)
    {
        var healthChecks = builder.Services.AddHealthChecks();

        AddProviderHealthCheck(healthChecks, provider, writeConnectionString, "write");

        if (!string.IsNullOrWhiteSpace(readConnectionString) &&
            !string.Equals(writeConnectionString, readConnectionString, StringComparison.OrdinalIgnoreCase))
        {
            AddProviderHealthCheck(healthChecks, provider, readConnectionString, "read");
        }

        return builder;
    }

    /// <summary>
    /// Adds PostgreSQL health checks for write and optional read endpoints.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="writeConnectionString">The write database connection string.</param>
    /// <returns>The same builder for chaining.</returns>
    public static WebApplicationBuilder AddPostgresReadWriteHealthChecks(
        this WebApplicationBuilder builder,
        string writeConnectionString)
    {
        return builder.AddReadWriteHealthChecks(DatabaseProvider.PostgreSQL, writeConnectionString, null);
    }

    /// <summary>
    /// Adds PostgreSQL health checks for write and optional read endpoints.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="writeConnectionString">The write database connection string.</param>
    /// <param name="readConnectionString">The optional read-only database connection string.</param>
    /// <returns>The same builder for chaining.</returns>
    public static WebApplicationBuilder AddPostgresReadWriteHealthChecks(
        this WebApplicationBuilder builder,
        string writeConnectionString,
        string? readConnectionString)
    {
        return builder.AddReadWriteHealthChecks(DatabaseProvider.PostgreSQL, writeConnectionString, readConnectionString);
    }

    private static void AddProviderHealthCheck(
        IHealthChecksBuilder healthChecks,
        DatabaseProvider provider,
        string connectionString,
        string role)
    {
        if (provider == DatabaseProvider.PostgreSQL)
        {
            healthChecks.AddNpgSql(connectionString, name: $"postgres-{role}", tags: new[] { "database", "postgres", role, "ready" });
            return;
        }

        if (provider == DatabaseProvider.SqlServer)
        {
            healthChecks.AddSqlServer(connectionString, name: $"sqlserver-{role}", tags: new[] { "database", "sqlserver", role, "ready" });
            return;
        }

        if (provider == DatabaseProvider.MySQL)
        {
            healthChecks.AddMySql(connectionString, name: $"mysql-{role}", tags: new[] { "database", "mysql", role, "ready" });
            return;
        }

        throw new ArgumentException($"Unsupported database provider '{provider.Name}'.", nameof(provider));
    }

    /// <summary>
    /// Adds a RabbitMQ health check.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="rabbitMqConnectionString">The RabbitMQ connection string.</param>
    /// <returns>The same builder for chaining.</returns>
    public static WebApplicationBuilder AddRabbitMqHealthCheck(this WebApplicationBuilder builder, string rabbitMqConnectionString)
    {
        builder.Services
            .AddHealthChecks()
            .AddRabbitMQ(
                _ =>
                {
                    var factory = new ConnectionFactory
                    {
                        Uri = new Uri(rabbitMqConnectionString),
                        AutomaticRecoveryEnabled = true,
                    };

                    return factory.CreateConnectionAsync();
                },
                timeout: TimeSpan.FromSeconds(5),
                tags: new[] { "messagebus", "rabbitmq", "ready" });

        return builder;
    }

    /// <summary>
    /// Adds a Redis health check.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="redisConnectionString">The Redis connection string.</param>
    /// <returns>The same builder for chaining.</returns>
    public static WebApplicationBuilder AddRedisHealthCheck(this WebApplicationBuilder builder, string redisConnectionString)
    {
        builder.Services
            .AddHealthChecks()
            .AddRedis(redisConnectionString, tags: new[] { "cache", "redis", "ready" });

        return builder;
    }
}
