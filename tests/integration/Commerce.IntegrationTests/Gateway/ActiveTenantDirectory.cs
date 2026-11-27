using SharedKernel.Core.MultiTenancy;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Test-only active directory for the single authenticated fixture tenant.</summary>
/// <param name="activeTenantId">The only tenant the customer principal belongs to.</param>
internal sealed class ActiveTenantDirectory(Guid activeTenantId) : ITenantDirectory
{
    /// <inheritdoc/>
    public Task<bool> IsActiveAsync(string tenantId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Guid.TryParseExact(tenantId, "D", out var candidate) && candidate == activeTenantId);
    }
}
