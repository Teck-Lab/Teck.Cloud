namespace Teck.Cloud.Commerce.Orders.Domain.Common;

/// <summary>Database offering tier. A dedicated tier is a tenant-specific database, not a cluster.</summary>
public enum Tier
{
    /// <summary>A database shared by tenants with row isolation.</summary>
    Shared = 1,

    /// <summary>A precreated tenant-specific database.</summary>
    Dedicated = 2,
}
