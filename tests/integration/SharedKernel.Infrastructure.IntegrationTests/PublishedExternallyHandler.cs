namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Actual RabbitMQ consumer.</summary>
public static class PublishedExternallyHandler
{
    /// <summary>Records the delivery in the EF transaction.</summary>
    /// <param name="message">External event.</param>
    /// <param name="context">The write context.</param>
    /// <param name="attempts">Controlled failure counter.</param>
    /// <exception cref="TimeoutException">The first attempt simulates a transient failure.</exception>
    public static void Handle(PublishedExternally message, MessageDbContext context, DeliveryAttempts attempts)
    {
        if (attempts.Increment(message.RecordId) == 1)
        {
            throw new TimeoutException("Controlled transient consumer failure.");
        }

        _ = context.Add(new Delivery { Id = message.RecordId });
    }
}
