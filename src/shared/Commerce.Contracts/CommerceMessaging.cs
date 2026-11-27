namespace Teck.Cloud.Commerce.Contracts;

/// <summary>Frozen RabbitMQ routes and wire values shared by Orders and Licensing.</summary>
public static class CommerceMessaging
{
    /// <summary>Orders-to-Licensing desired entitlement snapshot route.</summary>
    public const string LicenseDesired = "commerce.license-desired.v1";

    /// <summary>Licensing-to-Orders grant outcome route.</summary>
    public const string LicenseState = "commerce.license-state.v1";

    /// <summary>A tenant-isolated shared database offering.</summary>
    public const string SharedTier = "shared";

    /// <summary>A tenant-specific database offering; never a cluster.</summary>
    public const string DedicatedTier = "dedicated";

    /// <summary>An active entitlement outcome.</summary>
    public const string ActiveState = "active";

    /// <summary>An inactive entitlement outcome.</summary>
    public const string InactiveState = "inactive";
}
