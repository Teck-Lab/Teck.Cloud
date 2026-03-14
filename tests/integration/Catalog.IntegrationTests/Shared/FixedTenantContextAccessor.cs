using Finbuckle.MultiTenant.Abstractions;
using SharedKernel.Infrastructure.MultiTenant;

namespace Catalog.IntegrationTests.Shared;

internal sealed class FixedTenantContextAccessor(string tenantId = "test-tenant") : IMultiTenantContextAccessor<TenantDetails>
{
    public IMultiTenantContext<TenantDetails> MultiTenantContext { get; } = new MultiTenantContext<TenantDetails>(
        new TenantDetails
        {
            Id = tenantId,
            Identifier = tenantId,
            Name = "Test Tenant",
            IsActive = true,
        });

    IMultiTenantContext IMultiTenantContextAccessor.MultiTenantContext => MultiTenantContext;
}
