namespace Teck.Cloud.Commerce.Orders.Domain.Common;

/// <summary>Durable provider work kind.</summary>
public enum ReconciliationKind
{
    /// <summary>Create or recover the bound hosted checkout session.</summary>
    CheckoutSession = 1,

    /// <summary>Re-fetch and apply authoritative provider payment state.</summary>
    PaymentVerification = 2,

    /// <summary>Scheduled renewal or expiry check for an already verified paid period.</summary>
    PaymentRenewalCheck = 3,
}
