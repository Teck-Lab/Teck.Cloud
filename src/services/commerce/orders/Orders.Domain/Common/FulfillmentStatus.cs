namespace Teck.Cloud.Commerce.Orders.Domain.Common;

/// <summary>Asynchronous internal verification of recorded placement evidence.</summary>
public enum FulfillmentStatus
{
    /// <summary>No current placement evidence exists.</summary>
    AwaitingEvidence = 1,

    /// <summary>Evidence exists and verification has not produced a fresh usable result.</summary>
    Verifying = 2,

    /// <summary>A fresh probe verified the current evidence.</summary>
    Ready = 3,

    /// <summary>The current evidence failed terminally.</summary>
    Failed = 4,
}
