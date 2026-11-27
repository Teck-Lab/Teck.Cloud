namespace Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;

/// <summary>The database tier recorded in a signed license.</summary>
public enum LicenseTier
{
    /// <summary>A tenant-isolated database within the shared cluster.</summary>
    Shared = 0,

    /// <summary>A tenant-specific database, not a cluster.</summary>
    Dedicated = 1,
}
