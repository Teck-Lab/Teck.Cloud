namespace Teck.Cloud.Commerce.Orders.Domain.Common;

/// <summary>Readiness failure classification.</summary>
public enum FulfillmentFailureCode
{
    /// <summary>A transient probe failure; retryable and nonterminal.</summary>
    ProbeUnavailable = 1,

    /// <summary>The target binding does not match the recorded tenant/order/subscription/placement.</summary>
    BindingMismatch = 2,

    /// <summary>A required configured capability, such as a standby replica, is absent or mismatched.</summary>
    CapabilityUnavailable = 3,
}
