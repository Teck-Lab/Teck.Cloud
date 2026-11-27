namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.ApplyProbeOutcome.V1;

/// <summary>A bounded probe outcome classification.</summary>
public enum ProbeOutcomeKind
{
    /// <summary>A verified binding and capability set.</summary>
    Verified = 1,

    /// <summary>A retryable transport, credential or SQL availability failure.</summary>
    Unavailable = 2,

    /// <summary>A conclusive tenant/order/subscription/placement binding mismatch.</summary>
    BindingMismatch = 3,

    /// <summary>A required configured capability is absent or mismatched.</summary>
    CapabilityUnavailable = 4,
}
