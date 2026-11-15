using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.RabbitMQ;

namespace SharedKernel.Infrastructure.IdentityEvents;

/// <summary>Opt-in identity hints on an authenticated, publisher-restricted RabbitMQ endpoint.</summary>
public static class KeycloakEventExtensions
{
    /// <summary>Registers a dedicated durable listener; the host must configure Wolverine persistence and broker credentials.</summary>
    /// <param name="options">The owning host's Wolverine options.</param>
    /// <param name="queue">The separately permissioned identity queue.</param>
    /// <param name="source">Trusted issuer identity used to namespace event deduplication.</param>
    /// <param name="realm">The only permitted source realm.</param>
    public static void ListenToKeycloakOrganizationEvents(this WolverineOptions options, string queue, string source, string realm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queue);
        _ = options.Services.AddScoped<KeycloakTenantReconciler>();
        _ = options.Discovery.IncludeType(typeof(KeycloakOrganizationNoticeHandler));
        _ = options.ListenToRabbitQueue(queue)
            .UseInterop(new KeycloakEventMapper(source, realm, RabbitMqEndpointUri.Queue(queue)))
            .DefaultSerializer(new KeycloakEventSerializer())
            .DeadLetterQueueing(new DeadLetterQueue(queue + "-errors", DeadLetterQueueMode.WolverineStorage))
            .UseDurableInbox()
            .Sequential();
    }
}
