namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Stable internal failure codes returned by the placement probe adapter.</summary>
public static class PlacementFailureCodes
{
    /// <summary>The probe could not complete because of transport, credentials, permissions, SQL availability or configuration.</summary>
    public const string ProbeUnavailable = "probe_unavailable";

    /// <summary>The reachable target did not contain the exact tenant/order/subscription/placement marker.</summary>
    public const string BindingMismatch = "binding_mismatch";

    /// <summary>A required placement capability, tier, tenant scope, primary role, TLS or standby is absent or mismatched.</summary>
    public const string CapabilityUnavailable = "capability_unavailable";
}
