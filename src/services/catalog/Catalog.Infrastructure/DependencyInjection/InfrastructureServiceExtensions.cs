using System.Reflection;
using Catalog.Infrastructure.Persistence;
using JasperFx;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using Scrutor;
using SharedKernel.Core.Domain;
using SharedKernel.Core.Exceptions;
using SharedKernel.Core.Pricing;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Persistence.Database.Migrations;
using SharedKernel.Secrets;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

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
        Assembly dbContextAssembly = typeof(ApplicationWriteDbContext).Assembly;

        KeycloakAuthenticationOptions keycloakOptions = builder.Configuration.GetKeycloakOptions<KeycloakAuthenticationOptions>() ?? throw new ConfigurationMissingException("Keycloak");

        string rabbitmqConnectionString = builder.Configuration.GetConnectionString("rabbitmq") ?? throw new ConfigurationMissingException("RabbitMq");
        string defaultWriteConnectionString = builder.Configuration.GetConnectionString("postgres-write")
            ?? throw new ConfigurationMissingException("Database (write)");
        string defaultReadConnectionString = builder.Configuration.GetConnectionString("postgres-read")
            ?? defaultWriteConnectionString;
        builder.Services.AddKeycloak(builder.Configuration, builder.Environment, keycloakOptions);

        builder.AddCqrsDatabase(dbContextAssembly, defaultWriteConnectionString, defaultReadConnectionString);

        builder.UseWolverine(opts =>
        {
            opts.PersistMessagesWithPostgresql(defaultWriteConnectionString, schemaName: "wolverine")
                .UseMasterTableTenancy(data =>
                {
                    data.RegisterDefault(defaultWriteConnectionString);
                });

            opts.UseEntityFrameworkCoreTransactions();
            opts.PublishDomainEventsFromEntityFrameworkCore<BaseEntity>(entity => entity.DomainEvents);
            opts.Policies.UseDurableLocalQueues();

            var rabbit = opts.UseRabbitMq(new Uri(rabbitmqConnectionString));
            rabbit.AutoProvision();
            rabbit.EnableWolverineControlQueues();
            rabbit.UseConventionalRouting();

            opts.Services.AddDbContextWithWolverineManagedMultiTenancy<ApplicationWriteDbContext>(
                (builder, defaultWriteConnectionString, _) =>
            {
                builder.UseNpgsql(defaultWriteConnectionString.Value, assembly => assembly.MigrationsAssembly(dbContextAssembly));
            },
                AutoCreate.CreateOrUpdate);
        });
        builder.Services.AddHealthChecks().AddRabbitMQ(
            sp =>
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(rabbitmqConnectionString),
                AutomaticRecoveryEnabled = true
            };
            return factory.CreateConnectionAsync();
        },
            timeout: TimeSpan.FromSeconds(5),
            tags: ["messagebus", "rabbitmq"]);

        // Add Vault secrets management for database credentials
        builder.Services.AddVaultSecretsManagement(builder.Configuration);

        // Add multi-tenant migration services
        builder.Services.AddMultiTenantMigrations<ApplicationWriteDbContext>(
            DatabaseProvider.PostgreSQL);

        // Automatically register services.
        builder.Services.Scan(selector => selector
            .FromAssemblies(applicationAssembly, dbContextAssembly)
            .AddClasses(classes => classes.Where(type =>
                type != typeof(ApplicationReadDbContext) &&
                type != typeof(ApplicationWriteDbContext)))
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsMatchingInterface()
            .WithScopedLifetime());

        ////builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
    }

    /// <summary>
    /// Use catalog infrastructure.
    /// </summary>
    /// <param name="app">The app.</param>
    /// <returns>An IApplicationBuilder.</returns>
    public static IApplicationBuilder UseInfrastructureServices(this IApplicationBuilder app)
    {
        return app;
    }
}
