using Finbuckle.MultiTenant.Abstractions;
using SharedKernel.Infrastructure.MultiTenant;

namespace Customer.IntegrationTests.TestSupport;

internal static class TenantContextAccessorFactory
{
    public static IMultiTenantContextAccessor<TenantDetails> Create(Guid tenantId)
    {
        return new FixedTenantContextAccessor(tenantId);
    }

    private sealed class FixedTenantContextAccessor(Guid tenantId) : IMultiTenantContextAccessor<TenantDetails>
    {
        public IMultiTenantContext<TenantDetails> MultiTenantContext { get; } = new MultiTenantContext<TenantDetails>(
            new TenantDetails
            {
                Id = tenantId.ToString("D"),
                Identifier = tenantId.ToString("D"),
                Name = "Integration Tenant",
                IsActive = true,
            });

        IMultiTenantContext IMultiTenantContextAccessor.MultiTenantContext => this.MultiTenantContext;
    }
}
