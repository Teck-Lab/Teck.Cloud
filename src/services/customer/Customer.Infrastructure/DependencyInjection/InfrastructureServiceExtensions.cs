using System.Reflection;
using Customer.Application.Common.Interfaces;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using Customer.Infrastructure.Persistence;
using Customer.Infrastructure.Persistence.Repositories.Write;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using SharedKernel.Core.Domain;
using SharedKernel.Core.Exceptions;
using SharedKernel.Core.Pricing;
using SharedKernel.Persistence.Database.Migrations;
using SharedKernel.Secrets;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

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
        Assembly dbContextAssembly = typeof(CustomerWriteDbContext).Assembly;

        string rabbitmqConnectionString = builder.Configuration.GetConnectionString("rabbitmq")
            ?? throw new ConfigurationMissingException("RabbitMq");
        string defaultWriteConnectionString = builder.Configuration.GetConnectionString("postgres-write")
            ?? throw new ConfigurationMissingException("Database (write)");
        string defaultReadConnectionString = builder.Configuration.GetConnectionString("postgres-read")
            ?? defaultWriteConnectionString;

        // Add DbContexts
        builder.Services.AddDbContext<CustomerWriteDbContext>(
            options =>
                options.UseNpgsql(
                    defaultWriteConnectionString,
                    assembly => assembly.MigrationsAssembly(dbContextAssembly.FullName)));

        builder.Services.AddDbContext<CustomerReadDbContext>(
            options =>
                options.UseNpgsql(
                    defaultReadConnectionString,
                    assembly => assembly.MigrationsAssembly(dbContextAssembly.FullName)));

        // Register repositories
        builder.Services.AddScoped<ITenantWriteRepository, TenantWriteRepository>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Configure Wolverine
        builder.UseWolverine(opts =>
        {
            // Use dynamic type loading in development, static in production
            opts.CodeGeneration.TypeLoadMode = builder.Environment.IsDevelopment()
                ? JasperFx.CodeGeneration.TypeLoadMode.Dynamic
                : JasperFx.CodeGeneration.TypeLoadMode.Static;

            opts.PersistMessagesWithPostgresql(defaultWriteConnectionString, schemaName: "wolverine");

            opts.UseEntityFrameworkCoreTransactions();
            opts.PublishDomainEventsFromEntityFrameworkCore<BaseEntity>(entity => entity.DomainEvents);
            opts.Policies.UseDurableLocalQueues();

            var rabbit = opts.UseRabbitMq(new Uri(rabbitmqConnectionString));
            rabbit.AutoProvision();
            rabbit.EnableWolverineControlQueues();
            rabbit.UseConventionalRouting();
        });

        // Add health checks
        builder.Services.AddHealthChecks()
            .AddNpgSql(defaultWriteConnectionString, name: "postgres-write", tags: ["database", "postgres"])
            .AddRabbitMQ(
                serviceProvider =>
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
        builder.Services.AddMultiTenantMigrations<CustomerWriteDbContext>(
            DatabaseProvider.PostgreSQL);
    }

    /// <summary>
    /// Use customer infrastructure.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>An IApplicationBuilder.</returns>
    public static IApplicationBuilder UseInfrastructureServices(this IApplicationBuilder app)
    {
        return app;
    }
}
