// <copyright file="InfrastructureServiceExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Customer.Application.Common.Interfaces;
using Customer.Application.Tenants.Repositories;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using Customer.Infrastructure.Identity;
using Customer.Infrastructure.Persistence;
using Customer.Infrastructure.Persistence.Repositories.Read;
using Customer.Infrastructure.Persistence.Repositories.Write;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using SharedKernel.Core.Database;
using SharedKernel.Core.Exceptions;
using SharedKernel.Core.Pricing;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Database;
using SharedKernel.Infrastructure.HealthChecks;
using SharedKernel.Infrastructure.Messaging;
using SharedKernel.Persistence.Database;
using Wolverine;

namespace Customer.Infrastructure.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring infrastructure services for the Customer application.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Adds and configures infrastructure services for the Customer application.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance.</param>
    /// <param name="applicationAssembly">The application assembly to scan for services.</param>
    public static void AddInfrastructureServices(this WebApplicationBuilder builder, Assembly applicationAssembly)
    {
        ValidateInputs(builder, applicationAssembly);
        bool isRunningGeneration = CodeGenerationDetector.IsRunningGeneration();
        ConnectionSettings connectionSettings = ResolveConnectionSettings(builder.Configuration, isRunningGeneration);
        ConfigureIdentity(builder);
        ConfigureDbContexts(builder, connectionSettings);
        RegisterRepositories(builder.Services);
        ConfigureWolverine(builder, connectionSettings, isRunningGeneration);
        ConfigureHealthChecks(builder, connectionSettings, isRunningGeneration);
    }

    /// <summary>
    /// Use customer infrastructure.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>An IApplicationBuilder.</returns>
    public static IApplicationBuilder UseInfrastructureServices(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.ApplyLocalDatabaseMigrations<CustomerWriteDbContext, CustomerReadDbContext>();
        return app;
    }

    private static void ConfigureHealthChecks(WebApplicationBuilder builder, ConnectionSettings connectionSettings, bool isRunningGeneration)
    {
        if (isRunningGeneration)
        {
            return;
        }

        builder.AddRabbitMqHealthCheck(connectionSettings.RabbitConnectionString);
    }

    private static void ConfigureIdentity(WebApplicationBuilder builder)
    {
        if (!TryGetKeycloakOptions(builder.Configuration, out KeycloakAuthenticationOptions? keycloakOptions))
        {
            return;
        }

        if (keycloakOptions is null)
        {
            return;
        }

        SharedKernel.Infrastructure.Auth.Extensions.AddKeycloak(builder.Services, builder.Configuration, builder.Environment, keycloakOptions);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<ITenantReadRepository, TenantReadRepository>();
        services.AddScoped<ITenantWriteRepository, TenantWriteRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddHttpClient();
        services.AddScoped<ITenantIdentityProvisioningService, KeycloakTenantIdentityProvisioningService>();
    }

    private static void ConfigureDbContexts(WebApplicationBuilder builder, ConnectionSettings connectionSettings)
    {
        Assembly dbContextAssembly = typeof(CustomerWriteDbContext).Assembly;
        Assembly migrationsAssembly = ResolveMigrationsAssembly(connectionSettings.DatabaseProvider, dbContextAssembly);
        builder.AddCustomDbContexts<CustomerWriteDbContext, CustomerReadDbContext>(
            migrationsAssembly,
            connectionSettings.WriteConnectionString,
            connectionSettings.ReadConnectionString,
            connectionSettings.DatabaseProvider);
    }

    private static Assembly ResolveMigrationsAssembly(DatabaseProvider provider, Assembly fallbackAssembly)
    {
        string suffix;
        if (provider == DatabaseProvider.SqlServer)
        {
            suffix = "SqlServer";
        }
        else if (provider == DatabaseProvider.MySQL)
        {
            suffix = "MySql";
        }
        else
        {
            suffix = "PostgreSQL";
        }

        string assemblyName = $"Customer.Infrastructure.Migrations.{suffix}";
        Assembly? alreadyLoaded = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal));
        if (alreadyLoaded is not null)
        {
            return alreadyLoaded;
        }

        string assemblyPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll");
        if (File.Exists(assemblyPath))
        {
            AssemblyName migrationAssemblyName = new(assemblyName);
            return Assembly.Load(migrationAssemblyName);
        }

        return fallbackAssembly;
    }

    private static void ConfigureWolverine(WebApplicationBuilder builder, ConnectionSettings connectionSettings, bool isRunningGeneration)
    {
        bool isDevelopment = string.Equals(builder.Environment.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);

        builder.Host.UseWolverine(
            options =>
            {
                if (isRunningGeneration)
                {
                    options.CodeGeneration.TypeLoadMode = JasperFx.CodeGeneration.TypeLoadMode.Dynamic;
                    return;
                }

                WolverinePersistenceConfigurator.ConfigureStandardRuntime(
                    options,
                    isDevelopment,
                    connectionSettings.DatabaseProvider,
                    connectionSettings.WriteConnectionString,
                    connectionSettings.RabbitConnectionString);
            });
    }

    private static ConnectionSettings ResolveConnectionSettings(IConfiguration configuration, bool isRunningGeneration)
    {
        DatabaseProvider databaseProvider = configuration.GetDatabaseProvider();
        if (isRunningGeneration)
        {
            return CreateCodeGenerationConnectionSettings(databaseProvider);
        }

        string rabbitConnectionString = configuration.GetConnectionString("rabbitmq")
            ?? throw new ConfigurationMissingException("RabbitMq");
        string writeConnectionString = ResolveWriteConnectionString(configuration, databaseProvider);
        string readConnectionString = ResolveReadConnectionString(configuration, databaseProvider, writeConnectionString);

        string normalizedRabbitConnectionString = WolverinePersistenceConfigurator.NormalizeRabbitConnectionString(rabbitConnectionString);
        return new ConnectionSettings
        {
            RabbitConnectionString = normalizedRabbitConnectionString,
            WriteConnectionString = writeConnectionString,
            ReadConnectionString = readConnectionString,
            DatabaseProvider = databaseProvider,
        };
    }

    private static ConnectionSettings CreateCodeGenerationConnectionSettings(DatabaseProvider provider)
    {
        string placeholderConnectionString;
        if (provider == DatabaseProvider.SqlServer)
        {
            placeholderConnectionString = "Server=localhost,1433;Database=tempdb;User Id=sa;TrustServerCertificate=True";
        }
        else if (provider == DatabaseProvider.MySQL)
        {
            placeholderConnectionString = "Server=localhost;Port=3306;Database=tempdb;Uid=root;";
        }
        else
        {
            placeholderConnectionString = "Host=localhost;Port=5432;Database=tempdb;Username=postgres";
        }

        return new ConnectionSettings
        {
            RabbitConnectionString = "amqp://guest:guest@localhost:5672/",
            WriteConnectionString = placeholderConnectionString,
            ReadConnectionString = placeholderConnectionString,
            DatabaseProvider = provider,
        };
    }

    private static string ResolveWriteConnectionString(IConfiguration configuration, DatabaseProvider provider)
    {
        _ = provider;
        return configuration.GetConnectionString("db-write")
            ?? throw new ConfigurationMissingException("Database (write)");
    }

    private static string ResolveReadConnectionString(IConfiguration configuration, DatabaseProvider provider, string writeConnectionString)
    {
        string? readConnectionString = configuration.GetConnectionString("db-read");
        if (!string.IsNullOrWhiteSpace(readConnectionString))
        {
            return readConnectionString;
        }

        if (provider == DatabaseProvider.MySQL)
        {
            throw new ConfigurationMissingException("Database (read) for MySQL/MariaDB");
        }

        return writeConnectionString;
    }

    private static bool TryGetKeycloakOptions(ConfigurationManager configuration, out KeycloakAuthenticationOptions? keycloakOptions)
    {
        keycloakOptions = null;
        string? keycloakAuthServerUrl = configuration["Keycloak:AuthServerUrl"]
            ?? configuration["Keycloak:auth-server-url"]
            ?? configuration["Keycloak:Authority"];

        if (string.IsNullOrWhiteSpace(keycloakAuthServerUrl) ||
            !Uri.IsWellFormedUriString(keycloakAuthServerUrl, UriKind.Absolute))
        {
            return false;
        }

        keycloakOptions = configuration.GetKeycloakOptions<KeycloakAuthenticationOptions>();
        return keycloakOptions is not null &&
               !string.IsNullOrWhiteSpace(keycloakOptions.KeycloakUrlRealm) &&
               Uri.IsWellFormedUriString(keycloakOptions.KeycloakUrlRealm, UriKind.Absolute);
    }

    private static void ValidateInputs(WebApplicationBuilder builder, Assembly applicationAssembly)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(applicationAssembly);
    }

    private sealed record ConnectionSettings
    {
        public string RabbitConnectionString { get; init; } = string.Empty;

        public string WriteConnectionString { get; init; } = string.Empty;

        public string ReadConnectionString { get; init; } = string.Empty;

        public DatabaseProvider DatabaseProvider { get; init; } = default!;
    }
}
