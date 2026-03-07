using Finbuckle.MultiTenant.Abstractions;
using SharedKernel.Infrastructure.MultiTenant;

namespace Catalog.UnitTests.Infrastructure.Persistence;

internal static class TestTenantContextAccessor
{
    public static IMultiTenantContextAccessor<TenantDetails> Create(string tenantId = "test-tenant")
    {
        return new FixedTenantContextAccessor(tenantId);
    }

    private sealed class FixedTenantContextAccessor(string tenantId) : IMultiTenantContextAccessor<TenantDetails>
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
}