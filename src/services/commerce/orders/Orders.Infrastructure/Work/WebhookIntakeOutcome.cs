namespace Teck.Cloud.Commerce.Orders.Infrastructure.Work;

/// <summary>The webhook acknowledgement outcome.</summary>
public enum WebhookIntakeOutcome
{
    /// <summary>The event was durably accepted or already known.</summary>
    Accepted = 1,

    /// <summary>The signature, timestamp, account or mode was invalid.</summary>
    InvalidSignature = 2,

    /// <summary>The event was a duplicate receipt with no new effects.</summary>
    Duplicate = 3,
}
