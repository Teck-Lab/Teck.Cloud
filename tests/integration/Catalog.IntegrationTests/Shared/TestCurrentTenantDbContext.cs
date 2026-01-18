using SharedKernel.Persistence.Database.MultiTenant;

namespace Catalog.IntegrationTests.Shared
{
    // Simple test implementation for ICurrentTenantDbContext<TContext>
    public class TestCurrentTenantDbContext<TContext> : ICurrentTenantDbContext<TContext> where TContext : SharedKernel.Persistence.Database.EFCore.BaseDbContext
    {
        public TestCurrentTenantDbContext(TContext dbContext)
        {
            DbContext = dbContext;
        }

        public TContext DbContext { get; }
    }
}
