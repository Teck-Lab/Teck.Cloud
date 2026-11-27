namespace Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;

/// <summary>Lifecycle of one grant row for a desired entitlement generation.</summary>
public enum LicenseGrantState
{
    /// <summary>The grant is the active revision for its binding.</summary>
    Active = 0,

    /// <summary>Entitlement was disabled for this generation; no document is usable.</summary>
    Inactive = 1,

    /// <summary>A higher generation replaced this grant's document.</summary>
    Superseded = 2,
}
