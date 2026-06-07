#nullable enable
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Persistence.Database.EFCore.Interceptors;

namespace Catalog.IntegrationTests.Shared
{
    /// <summary>
    /// Base test fixture for testing read repositories with shared-database table-truncation isolation.
    /// </summary>
    public abstract class BaseReadRepoTestFixture<TContext> : IAsyncLifetime where TContext : DbContext
    {
        protected readonly SharedTestcontainersFixture SharedFixture;
        protected TContext ReadDbContext = null!;
        protected SoftDeleteInterceptor SoftDeleteInterceptor = null!;
        protected AuditingInterceptor AuditingInterceptor = null!;
        protected IServiceProvider ServiceProvider = null!;
        protected string? ConnectionString;

        protected BaseReadRepoTestFixture(SharedTestcontainersFixture sharedFixture)
        {
            SharedFixture = sharedFixture;
        }

        public virtual async ValueTask InitializeAsync()
        {
            var httpContextAccessor = new HttpContextAccessor();
            var services = new ServiceCollection();
            services.AddSingleton<IHttpContextAccessor>(httpContextAccessor);
            ServiceProvider = services.BuildServiceProvider();

            SoftDeleteInterceptor = new SoftDeleteInterceptor(httpContextAccessor);
            AuditingInterceptor = new AuditingInterceptor(httpContextAccessor);

            // Create or reuse a shared test database using shared fixture
            ConnectionString = await SharedFixture.CreateSharedTestDatabaseAsync(
                typeof(TContext),
                "Catalog.Infrastructure.Migrations.PostgreSQL",
                TestContext.Current.CancellationToken);

            var optionsBuilder = new DbContextOptionsBuilder<TContext>()
                .UseNpgsql(ConnectionString)
                .AddInterceptors(SoftDeleteInterceptor, AuditingInterceptor);

            var options = optionsBuilder.Options;
            var tenantAccessor = new FixedTenantContextAccessor();
            ReadDbContext = CreateReadDbContext(options, tenantAccessor);

            await SeedAsync();
        }

        public virtual async ValueTask DisposeAsync()
        {
            try
            {
                if (ReadDbContext != null)
                {
                    await ReadDbContext.DisposeAsync();
                }

                if (ConnectionString is not null)
                {
                    await SharedFixture.TruncateAllTablesAsync(ConnectionString, TestContext.Current.CancellationToken);
                }
            }
            catch
            {
                // best-effort cleanup
            }
        }

        protected abstract TContext CreateReadDbContext(DbContextOptions<TContext> options, IMultiTenantContextAccessor<TenantDetails> tenantAccessor);

        /// <summary>
        /// Optional: Override to seed data before each test.
        /// </summary>
        protected virtual Task SeedAsync() => Task.CompletedTask;
    }
}
