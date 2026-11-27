namespace Teck.Cloud.Commerce.Orders.Domain.Common;

/// <summary>Durable signed-webhook receipt state.</summary>
public enum ProviderReceiptStatus
{
    /// <summary>The signed event was accepted and queued.</summary>
    Received = 1,

    /// <summary>The event is being reconciled.</summary>
    Processing = 2,

    /// <summary>The event completed without duplicate effects.</summary>
    Completed = 3,

    /// <summary>The event could not be applied and requires operator attention.</summary>
    Failed = 4,

    /// <summary>The event was verified but no bound order matched it.</summary>
    Unmatched = 5,
}
