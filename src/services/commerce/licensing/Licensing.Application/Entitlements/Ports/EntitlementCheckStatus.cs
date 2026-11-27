namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.Ports;

/// <summary>Authoritative answer from Orders' current durable entitlement facts.</summary>
public enum EntitlementCheckStatus
{
    /// <summary>Every current gate passes for the exact binding and generation.</summary>
    Eligible = 0,

    /// <summary>Orders is reachable and reports the generation is not entitled.</summary>
    NotEligible = 1,

    /// <summary>Orders could not produce an authoritative answer; callers must fail closed.</summary>
    Unavailable = 2,
}
