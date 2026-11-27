namespace Teck.Cloud.Commerce.Orders.Domain.Common;

/// <summary>Verified provider payment state.</summary>
public enum PaymentStatus
{
    /// <summary>No current authoritative paid period.</summary>
    Pending = 1,

    /// <summary>A current authoritative paid period is verified.</summary>
    Paid = 2,
}
