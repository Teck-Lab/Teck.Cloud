using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Core.Events;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.Health;
using SharedKernel.Persistence.Database;
using Wolverine;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace SharedKernel.Infrastructure.Messaging;

/// <summary>Explicit durable local and external routing; no catch-all broker publication.</summary>
public static class MessagingExtensions
{
    /// <summary>Configures a durable PostgreSQL inbox/outbox and EF event collection.</summary>
    /// <typeparam name="TWrite">The concrete service write context.</typeparam>
    /// <param name="options">Wolverine options.</param>
    /// <param name="connectionString">Write-capable service database connection.</param>
    /// <param name="schema">Service-owned messaging schema.</param>
    public static void UseFoundationMessaging<TWrite>(this WolverineOptions options, string connectionString, string schema)
        where TWrite : WriteDbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        _ = options.PersistMessagesWithPostgresql(connectionString, schema);
        options.UseFoundationEfTransactions<TWrite>();
        _ = options.Services.AddHealthChecks().AddNpgSql(connectionString, name: "postgres-messaging", tags: FoundationHealthChecks.DependencyTags, timeout: TimeSpan.FromSeconds(5));

        // Middleware and EF must share the same scoped identity, not separate inlined instances.
        _ = options.CodeGeneration.AlwaysUseServiceLocationFor<OperationContext>();
        options.Policies.AddMiddleware(typeof(TenantMessageMiddleware), chain => typeof(ITenantMessage).IsAssignableFrom(chain.MessageType));
        options.Policies.AddMiddleware(typeof(GlobalMessageMiddleware), chain => typeof(IGlobalMessage).IsAssignableFrom(chain.MessageType));
        options.Policies.UseDurableOutboxOnAllSendingEndpoints();
        options.Policies.UseDurableInboxOnAllListeners();
    }

    /// <summary>Enables a required broker and RabbitMQ coordination between Wolverine nodes.</summary>
    /// <param name="options">Wolverine options.</param>
    /// <param name="broker">The secret-supplied RabbitMQ URI.</param>
    /// <exception cref="ArgumentException">The broker URI has an unsupported scheme.</exception>
    public static void UseRequiredRabbitMq(this WolverineOptions options, Uri broker)
    {
        ArgumentNullException.ThrowIfNull(broker);
        if (broker.Scheme is not ("amqp" or "amqps"))
        {
            throw new ArgumentException("RabbitMQ requires an amqp or amqps URI.", nameof(broker));
        }

        _ = options.UseRabbitMq(broker).EnableWolverineControlQueues();
        _ = options.Services.AddSingleton(_ => new RabbitMqHealthConnection(broker));
    }

    /// <summary>Routes a domain event to a durable in-process queue.</summary>
    /// <typeparam name="TEvent">The service-local event.</typeparam>
    /// <param name="options">Wolverine options.</param>
    /// <param name="queue">The local queue name.</param>
    public static void RouteDomainEvent<TEvent>(this WolverineOptions options, string queue)
        where TEvent : IDomainEvent
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queue);
        _ = options.PublishMessage<TEvent>().ToLocalQueue(queue);
    }

    /// <summary>Routes a separate integration contract to a durable RabbitMQ exchange.</summary>
    /// <typeparam name="TEvent">The integration contract.</typeparam>
    /// <param name="options">Wolverine options.</param>
    /// <param name="exchange">The explicit exchange name.</param>
    /// <exception cref="InvalidOperationException">A domain event is also marked as an integration event.</exception>
    public static void RouteIntegrationEvent<TEvent>(this WolverineOptions options, string exchange)
        where TEvent : IIntegrationEvent
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        if (typeof(IDomainEvent).IsAssignableFrom(typeof(TEvent)))
        {
            throw new InvalidOperationException("Translate domain events into separate integration contracts.");
        }

        _ = options.PublishMessage<TEvent>().ToRabbitExchange(exchange).UseDurableOutbox();
    }
}
