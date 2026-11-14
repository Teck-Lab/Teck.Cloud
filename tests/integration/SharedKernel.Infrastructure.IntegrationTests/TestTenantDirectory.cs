using SharedKernel.Core.MultiTenancy;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Controlled active-tenant fixture.</summary>
public sealed class TestTenantDirectory : ITenantDirectory
{
    /// <inheritdoc/>
    public Task<bool> IsActiveAsync(string tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(string.Equals(tenantId, "alpha", StringComparison.Ordinal) || string.Equals(tenantId, "beta", StringComparison.Ordinal));
}
