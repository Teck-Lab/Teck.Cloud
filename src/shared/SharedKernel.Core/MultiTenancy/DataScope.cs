namespace SharedKernel.Core.MultiTenancy;

/// <summary>An explicitly global or tenant-owned operation scope.</summary>
public sealed record DataScope
{
    private DataScope(string? tenantId)
    {
        TenantId = tenantId;
    }

    /// <summary>Gets the tenant identifier, or null for an explicitly global scope.</summary>
    public string? TenantId { get; }

    /// <summary>Gets the global data scope.</summary>
    public static DataScope Global { get; } = new((string?)null);

    /// <summary>Creates a tenant data scope from an already authorized identity.</summary>
    /// <param name="tenantId">The validated tenant identifier.</param>
    /// <returns>The tenant scope.</returns>
    public static DataScope ForTenant(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return new(tenantId);
    }
}
