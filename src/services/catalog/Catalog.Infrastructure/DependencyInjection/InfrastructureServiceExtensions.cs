// <copyright file="InfrastructureServiceExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Catalog.Infrastructure.Persistence;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Scrutor;
using SharedKernel.Core.Exceptions;
using SharedKernel.Core.Pricing;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.Database;
using SharedKernel.Infrastructure.HealthChecks;
using SharedKernel.Infrastructure.Messaging;
using SharedKernel.Persistence.Database;
using Wolverine;

namespace Catalog.Infrastructure.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring infrastructure services for the Catalog application.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Adds and configures infrastructure services for the Catalog application.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance.</param>
    /// <param name="applicationAssembly">The application assembly to scan for services.</param>
    public static void AddInfrastructureServices(this WebApplicationBuilder builder, Assembly applicationAssembly)
    {
        ValidateInputs(builder, applicationAssembly);
        bool isRunningGeneration = CodeGenerationDetector.IsRunningGeneration();
        CatalogConnectionSettings connectionSettings = ResolveConnectionSettings(builder.Configuration, isRunningGeneration);

        ConfigureIdentity(builder);
        ConfigureDatabase(builder, connectionSettings);
        ConfigureWolverine(builder, connectionSettings, isRunningGeneration);
        ConfigureHealthChecks(builder, connectionSettings, isRunningGeneration);
        RegisterServices(builder.Services, applicationAssembly);
    }

    /// <summary>
    /// Use catalog infrastructure.
    /// </summary>
    /// <param name="app">The app.</param>
    /// <returns>An IApplicationBuilder.</returns>
    public static IApplicationBuilder UseInfrastructureServices(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.ApplyLocalDatabaseMigrations<ApplicationWriteDbContext, ApplicationReadDbContext>();
        return app;
    }

    private static string ResolveWriteConnectionString(IConfiguration configuration, DatabaseProvider provider)
    {
        _ = provider;
        return configuration.GetConnectionString("db-write")
            ?? throw new ConfigurationMissingException("Database (write)");
    }

    private static string ResolveReadConnectionString(IConfiguration configuration, DatabaseProvider provider, string defaultWriteConnectionString)
    {
        _ = provider;
        return configuration.GetConnectionString("db-read")
            ?? defaultWriteConnectionString;
    }

    private static void ConfigureIdentity(WebApplicationBuilder builder)
    {
        if (!TryGetKeycloakOptions(builder.Configuration, out KeycloakAuthenticationOptions? keycloakOptions) || keycloakOptions is null)
        {
            Console.WriteLine("[Startup] Keycloak not configured or authority invalid; skipping Keycloak registration for tests.");
            return;
        }

        builder.Services.AddKeycloak(builder.Configuration, builder.Environment, keycloakOptions);
    }

    private static void ConfigureDatabase(WebApplicationBuilder builder, CatalogConnectionSettings settings)
    {
        Assembly dbContextAssembly = typeof(ApplicationWriteDbContext).Assembly;
        Assembly migrationsAssembly = ResolveMigrationsAssembly(settings.DatabaseProvider, dbContextAssembly);

        builder.AddCqrsDatabase(
            migrationsAssembly,
            settings.WriteConnectionString,
            settings.ReadConnectionString,
            settings.DatabaseProvider);
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

        string assemblyName = $"Catalog.Infrastructure.Migrations.{suffix}";
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

    private static void ConfigureWolverine(WebApplicationBuilder builder, CatalogConnectionSettings settings, bool isRunningGeneration)
    {
        bool isDevelopment = builder.Environment.IsDevelopment();

        builder.UseWolverine(options =>
        {
            if (isRunningGeneration)
            {
                options.CodeGeneration.TypeLoadMode = JasperFx.CodeGeneration.TypeLoadMode.Dynamic;
                return;
            }

            WolverinePersistenceConfigurator.ConfigureStandardRuntime(
                options,
                isDevelopment,
                settings.DatabaseProvider,
                settings.WriteConnectionString,
                settings.RabbitConnectionString);

            Console.WriteLine($"[Startup] Using RabbitMQ URI for Wolverine: {settings.RabbitConnectionString}");
        });
    }

    private static void ConfigureHealthChecks(WebApplicationBuilder builder, CatalogConnectionSettings settings, bool isRunningGeneration)
    {
        if (isRunningGeneration)
        {
            return;
        }

        builder.AddRabbitMqHealthCheck(settings.RabbitConnectionString);
    }

    private static void RegisterServices(IServiceCollection services, Assembly applicationAssembly)
    {
        Assembly dbContextAssembly = typeof(ApplicationWriteDbContext).Assembly;

        services.Scan(selector => selector
            .FromAssemblies(applicationAssembly, dbContextAssembly)
            .AddClasses(classes => classes.Where(type =>
                type != typeof(ApplicationReadDbContext) &&
                type != typeof(ApplicationWriteDbContext)))
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsMatchingInterface()
            .WithScopedLifetime());
    }

    private static CatalogConnectionSettings ResolveConnectionSettings(IConfiguration configuration, bool isRunningGeneration)
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

        return new CatalogConnectionSettings
        {
            RabbitConnectionString = WolverinePersistenceConfigurator.NormalizeRabbitConnectionString(rabbitConnectionString),
            WriteConnectionString = writeConnectionString,
            ReadConnectionString = readConnectionString,
            DatabaseProvider = databaseProvider,
        };
    }

    private static CatalogConnectionSettings CreateCodeGenerationConnectionSettings(DatabaseProvider provider)
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

        return new CatalogConnectionSettings
        {
            RabbitConnectionString = "amqp://guest:guest@localhost:5672/",
            WriteConnectionString = placeholderConnectionString,
            ReadConnectionString = placeholderConnectionString,
            DatabaseProvider = provider,
        };
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

    private sealed record CatalogConnectionSettings
    {
        public string RabbitConnectionString { get; init; } = string.Empty;

        public string WriteConnectionString { get; init; } = string.Empty;

        public string ReadConnectionString { get; init; } = string.Empty;

        public DatabaseProvider DatabaseProvider { get; init; } = default!;
    }
}
