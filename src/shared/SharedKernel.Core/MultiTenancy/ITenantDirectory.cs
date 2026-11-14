namespace SharedKernel.Core.MultiTenancy;

/// <summary>Service-owned lookup for known, active tenants; no default tenant is synthesized.</summary>
public interface ITenantDirectory
{
    /// <summary>Checks whether an already-authorized tenant still exists and is active.</summary>
    /// <param name="tenantId">The validated tenant identity.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Whether the tenant is usable.</returns>
    Task<bool> IsActiveAsync(string tenantId, CancellationToken cancellationToken);
}
