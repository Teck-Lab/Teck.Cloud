// <copyright file="InfrastructureServiceExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

// Suppress IDE0005 (unused using) here because repository types are registered via source-generated
// methods and their namespaces may appear unused to the analyzer.
using System.Reflection;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Product.Infrastructure.Persistence;
using SharedKernel.Core.Database;
using SharedKernel.Core.Exceptions;
using SharedKernel.Core.Pricing;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Persistence.Database;
using SharedKernel.Persistence.Database.EFCore;

namespace Product.Infrastructure.DependencyInjection;

/// <summary>
/// Registers Product infrastructure services.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Adds Product infrastructure dependencies.
    /// </summary>
    /// <param name="builder">Application builder.</param>
    /// <param name="applicationAssembly">Application assembly.</param>
    public static void AddInfrastructureServices(this WebApplicationBuilder builder, Assembly applicationAssembly)
    {
        ValidateInputs(builder, applicationAssembly);
        bool isRunningGeneration = CodeGenerationDetector.IsRunningGeneration();
        ProductConnectionSettings connectionSettings = ResolveConnectionSettings(builder.Configuration, isRunningGeneration);

        ConfigureDatabase(builder, connectionSettings);
        ConfigureIdentity(builder);

        // Register repositories via source-generated registrations (replaces manual AddScoped registrations)
        builder.Services.AddProductInfrastructureRepositories();
    }

#pragma warning restore IDE0005

    /// <summary>
    /// Adds Product infrastructure middleware.
    /// </summary>
    /// <param name="app">Application builder.</param>
    /// <returns>Configured application builder.</returns>
    public static IApplicationBuilder UseInfrastructureServices(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app;
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

    private static void ConfigureDatabase(WebApplicationBuilder builder, ProductConnectionSettings settings)
    {
        Assembly? migrationsAssembly = null;

        builder.AddCqrsDatabase(
            migrationsAssembly,
            settings.WriteConnectionString,
            settings.ReadConnectionString,
            settings.DatabaseProvider);
        builder.Services.AddScoped<IUnitOfWork>(sp =>
        {
            var writeDbContext = sp.GetRequiredService<ProductWriteDbContext>();
            return new UnitOfWork<ProductWriteDbContext>(writeDbContext);
        });
    }

    private static ProductConnectionSettings ResolveConnectionSettings(IConfiguration configuration, bool isRunningGeneration)
    {
        DatabaseProvider databaseProvider = configuration.GetDatabaseProvider();
        if (isRunningGeneration)
        {
            return CreateCodeGenerationConnectionSettings(databaseProvider);
        }

        return new ProductConnectionSettings
        {
            WriteConnectionString = ResolveWriteConnectionString(configuration, databaseProvider),
            ReadConnectionString = ResolveReadConnectionString(configuration, databaseProvider),
            DatabaseProvider = databaseProvider,
        };
    }

    private static string ResolveReadConnectionString(IConfiguration configuration, DatabaseProvider provider)
    {
        _ = provider;
        return configuration.GetConnectionString("db-read")
            ?? ResolveWriteConnectionString(configuration, provider);
    }

    private static string ResolveWriteConnectionString(IConfiguration configuration, DatabaseProvider provider)
    {
        _ = provider;
        return configuration.GetConnectionString("db-write")
            ?? throw new ConfigurationMissingException("Database (write)");
    }

    private static ProductConnectionSettings CreateCodeGenerationConnectionSettings(DatabaseProvider provider)
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

        return new ProductConnectionSettings
        {
            WriteConnectionString = placeholderConnectionString,
            ReadConnectionString = placeholderConnectionString,
            DatabaseProvider = provider,
        };
    }

    private sealed record ProductConnectionSettings
    {
        public string WriteConnectionString { get; init; } = string.Empty;

        public string ReadConnectionString { get; init; } = string.Empty;

        public DatabaseProvider DatabaseProvider { get; init; } = default!;
    }
}
