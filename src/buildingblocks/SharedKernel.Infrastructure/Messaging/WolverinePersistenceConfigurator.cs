using JasperFx;
using JasperFx.CodeGeneration;
using SharedKernel.Core.Domain;
using SharedKernel.Core.Pricing;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.MemoryPack;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;
using Wolverine.SqlServer;

namespace SharedKernel.Infrastructure.Messaging;

/// <summary>
/// Configures Wolverine durable persistence by selected database provider.
/// </summary>
public static class WolverinePersistenceConfigurator
{
    private const string WolverineSchemaName = "wolverine";

    /// <summary>
    /// Normalizes RabbitMQ connection-string schemes to AMQP-compatible URIs.
    /// </summary>
    /// <param name="rabbitConnectionString">The configured RabbitMQ connection string.</param>
    /// <returns>A normalized AMQP/AMQPS URI string.</returns>
    public static string NormalizeRabbitConnectionString(string rabbitConnectionString)
    {
        if (rabbitConnectionString.StartsWith("rabbitmqs://", StringComparison.OrdinalIgnoreCase))
        {
            return "amqps://" + rabbitConnectionString["rabbitmqs://".Length..];
        }

        if (rabbitConnectionString.StartsWith("rabbitmq://", StringComparison.OrdinalIgnoreCase))
        {
            return "amqp://" + rabbitConnectionString["rabbitmq://".Length..];
        }

        return rabbitConnectionString.Trim();
    }

    /// <summary>
    /// Configures common Wolverine runtime setup shared by services.
    /// </summary>
    /// <param name="options">The Wolverine options.</param>
    /// <param name="isDevelopment">Whether the hosting environment is development.</param>
    /// <param name="provider">The selected database provider.</param>
    /// <param name="writeConnectionString">The write database connection string.</param>
    /// <param name="rabbitConnectionString">The normalized RabbitMQ connection string.</param>
    public static void ConfigureStandardRuntime(
        WolverineOptions options,
        bool isDevelopment,
        DatabaseProvider provider,
        string writeConnectionString,
        string rabbitConnectionString)
    {
        options.CodeGeneration.TypeLoadMode = isDevelopment
            ? TypeLoadMode.Dynamic
            : TypeLoadMode.Static;

        ConfigureDatabasePersistence(options, provider, writeConnectionString);
        options.AutoBuildMessageStorageOnStartup = AutoCreate.None;
        options.UseMemoryPackSerialization();

        options.UseEntityFrameworkCoreTransactions();
        options.PublishDomainEventsFromEntityFrameworkCore<BaseEntity>(entity => entity.DomainEvents);
        options.Policies.UseDurableLocalQueues();

        var rabbit = options.UseRabbitMq(new Uri(rabbitConnectionString, UriKind.Absolute));
        rabbit.AutoProvision();
        rabbit.EnableWolverineControlQueues();
        rabbit.UseConventionalRouting();
    }

    /// <summary>
    /// Configures Wolverine message persistence with the selected provider.
    /// </summary>
    /// <param name="options">The Wolverine options.</param>
    /// <param name="provider">The selected database provider.</param>
    /// <param name="writeConnectionString">The write connection string.</param>
    public static void ConfigureDatabasePersistence(
        WolverineOptions options,
        DatabaseProvider provider,
        string writeConnectionString)
    {
        if (provider == DatabaseProvider.PostgreSQL)
        {
            options
                .PersistMessagesWithPostgresql(writeConnectionString, WolverineSchemaName)
                .OverrideAutoCreateResources(AutoCreate.CreateOrUpdate);
            return;
        }

        if (provider == DatabaseProvider.SqlServer)
        {
            options
                .PersistMessagesWithSqlServer(writeConnectionString, WolverineSchemaName)
                .OverrideAutoCreateResources(AutoCreate.CreateOrUpdate);
            return;
        }

        if (provider == DatabaseProvider.MySQL)
        {
            throw new System.InvalidOperationException(
                "Wolverine MySQL persistence is not configured. Add explicit MySQL Wolverine persistence support before using DatabaseProvider.MySQL.");
        }

        throw new System.InvalidOperationException($"Unsupported database provider '{provider.Name}'.");
    }
}
