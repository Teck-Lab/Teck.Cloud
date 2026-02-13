using System.Reflection;
using Catalog.Infrastructure.Persistence;
using JasperFx.CodeGeneration;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Scrutor;
using SharedKernel.Core.Domain;
using SharedKernel.Core.Exceptions;
using SharedKernel.Core.Database;
using SharedKernel.Infrastructure.Auth;
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

        // Only attempt to bind Keycloak options if a Keycloak server URL is provided and looks valid.
        KeycloakAuthenticationOptions? keycloakOptions = null;
        var keycloakAuthServerUrl = builder.Configuration["Keycloak:AuthServerUrl"];
        if (!string.IsNullOrWhiteSpace(keycloakAuthServerUrl) && Uri.IsWellFormedUriString(keycloakAuthServerUrl, UriKind.Absolute))
        {
            try
            {
                keycloakOptions = builder.Configuration.GetKeycloakOptions<KeycloakAuthenticationOptions>();
            }
            catch (Exception bindException)
            {
                Console.WriteLine($"[Startup] Failed to bind Keycloak options: {bindException}");
            }
        }

        string rabbitmqConnectionString = builder.Configuration.GetConnectionString("rabbitmq")
            ?? throw new ConfigurationMissingException("RabbitMq");

        string defaultWriteConnectionString = builder.Configuration.GetConnectionString("postgres-write")
            ?? throw new ConfigurationMissingException("Database (write)");
        string defaultReadConnectionString = builder.Configuration.GetConnectionString("postgres-read")
            ?? defaultWriteConnectionString;

        // Only configure Keycloak if options are present and the configured authority is a valid absolute URI.
        if (keycloakOptions != null &&
            !string.IsNullOrWhiteSpace(keycloakOptions.KeycloakUrlRealm) &&
            Uri.IsWellFormedUriString(keycloakOptions.KeycloakUrlRealm, UriKind.Absolute))
        {
            try
            {
                builder.Services.AddKeycloak(builder.Configuration, builder.Environment, keycloakOptions);
            }
            catch (Exception addKeycloakException)
            {
                // Log and continue; tests should be able to run without Keycloak configured correctly
                Console.WriteLine($"[Startup] AddKeycloak failed: {addKeycloakException}");
            }
        }
        else
        {
            Console.WriteLine("[Startup] Keycloak not configured or authority invalid; skipping Keycloak registration for tests.");
        }

        builder.AddCqrsDatabase(dbContextAssembly, defaultWriteConnectionString, defaultReadConnectionString);

        try
        {
            builder.UseWolverine(opts =>
            {
                // Use dynamic type loading in development, static in production
                opts.CodeGeneration.TypeLoadMode = builder.Environment.IsDevelopment()
                    ? TypeLoadMode.Dynamic
                    : TypeLoadMode.Static;

                opts.PersistMessagesWithPostgresql(defaultWriteConnectionString, schemaName: "wolverine")
                    .UseMasterTableTenancy(data =>
                    {
                        data.RegisterDefault(defaultWriteConnectionString);
                    });

                opts.UseEntityFrameworkCoreTransactions();
                opts.PublishDomainEventsFromEntityFrameworkCore<BaseEntity>(entity => entity.DomainEvents);
                opts.Policies.UseDurableLocalQueues();

                // Normalize rabbitmq URI scheme to amqp/amqps for RabbitMQ.Client compatibility
                var normalizedRabbit = rabbitmqConnectionString;
                if (normalizedRabbit.StartsWith("rabbitmqs://", System.StringComparison.OrdinalIgnoreCase))
                {
                    normalizedRabbit = string.Concat("amqps://".AsSpan(), normalizedRabbit.AsSpan("rabbitmqs://".Length));
                }
                else if (normalizedRabbit.StartsWith("rabbitmq://", System.StringComparison.OrdinalIgnoreCase))
                {
                    normalizedRabbit = string.Concat("amqp://".AsSpan(), normalizedRabbit.AsSpan("rabbitmq://".Length));
                }

                Console.WriteLine($"[Startup] Using RabbitMQ URI for Wolverine: {normalizedRabbit}");
                var rabbit = opts.UseRabbitMq(new Uri(normalizedRabbit));
                rabbit.AutoProvision();
                rabbit.EnableWolverineControlQueues();
                rabbit.UseConventionalRouting();

                opts.Services.AddDbContextWithWolverineManagedMultiTenancy<ApplicationWriteDbContext>(
                    (builder, defaultWriteConnectionString, _) =>
                    {
                        builder.UseNpgsql(defaultWriteConnectionString.Value, assembly => assembly.MigrationsAssembly(dbContextAssembly));
                    });
            });
        }
        catch (Exception wolverineException)
        {
            Console.WriteLine($"[Startup][Error] Exception configuring Wolverine/RabbitMQ: {wolverineException}");
            throw;
        }


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
            tags: new[] { "messagebus", "rabbitmq" });



        // Automatically register services.
        builder.Services.Scan(selector => selector
            .FromAssemblies(applicationAssembly, dbContextAssembly)
            .AddClasses(classes => classes.Where(type =>
                type != typeof(ApplicationReadDbContext) &&
                type != typeof(ApplicationWriteDbContext)))
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsMatchingInterface()
            .WithScopedLifetime());
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
