namespace Teck.Cloud.Commerce.Orders.Domain.Common;

/// <summary>Lifecycle of the desired entitlement generation.</summary>
public enum LicenseState
{
    /// <summary>No outcome is recorded for the current generation.</summary>
    Pending = 1,

    /// <summary>Licensing confirmed an active grant for the current generation.</summary>
    Active = 2,

    /// <summary>Licensing confirmed a disabled or superseded generation.</summary>
    Inactive = 3,

    /// <summary>The current generation was explicitly revoked.</summary>
    Revoked = 4,
}
