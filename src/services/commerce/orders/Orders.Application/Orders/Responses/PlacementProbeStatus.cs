namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>The classified outcome of one read-only placement probe.</summary>
public enum PlacementProbeStatus
{
    /// <summary>The target is an allowlisted, TLS, read-only binding probe that matched every required fact.</summary>
    Verified = 0,

    /// <summary>The probe could not complete; this is retryable probe_unavailable, never terminal.</summary>
    Unavailable = 1,

    /// <summary>The target was reachable but the binding does not match; terminal binding_mismatch.</summary>
    BindingMismatch = 2,

    /// <summary>A required capability, tier, role or configuration is absent or mismatched; terminal capability_unavailable.</summary>
    CapabilityUnavailable = 3,
}
