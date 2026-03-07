#nullable enable
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Finbuckle.MultiTenant.Abstractions;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Persistence.Database.EFCore.Interceptors;

namespace Catalog.IntegrationTests.Shared
{
    /// <summary>
    /// Base test fixture for testing read repositories
    /// </summary>
    public abstract class BaseReadRepoTestFixture<TContext> : IAsyncLifetime where TContext : DbContext
    {
        protected readonly SharedTestcontainersFixture SharedFixture;
        protected TContext ReadDbContext = null!;
        protected SoftDeleteInterceptor SoftDeleteInterceptor = null!;
        protected AuditingInterceptor AuditingInterceptor = null!;
        protected IServiceProvider ServiceProvider = null!;

        protected BaseReadRepoTestFixture(SharedTestcontainersFixture sharedFixture)
        {
            SharedFixture = sharedFixture;
        }

        public virtual async ValueTask InitializeAsync()
        {
            // Containers are already started by the shared fixture
            var httpContextAccessor = new HttpContextAccessor();
            var services = new ServiceCollection();
            services.AddSingleton<IHttpContextAccessor>(httpContextAccessor);
            ServiceProvider = services.BuildServiceProvider();

            SoftDeleteInterceptor = new SoftDeleteInterceptor(httpContextAccessor);
            AuditingInterceptor = new AuditingInterceptor(httpContextAccessor);

            var optionsBuilder = new DbContextOptionsBuilder<TContext>()
                .AddInterceptors(SoftDeleteInterceptor, AuditingInterceptor);

            if (SharedFixture.UseSqliteFallback)
            {
                optionsBuilder.UseSqlite(SharedFixture.SqliteConnection!);
            }
            else
            {
                optionsBuilder.UseNpgsql(SharedFixture.DbContainer!.GetConnectionString());
            }

            var options = optionsBuilder.Options;
            var tenantAccessor = new FixedTenantContextAccessor();
            ReadDbContext = CreateReadDbContext(options, tenantAccessor);

            await ReadDbContext.Database.EnsureCreatedAsync();
            await SeedAsync();
        }

        public virtual async ValueTask DisposeAsync()
        {
            // Do NOT dispose containers here; handled by shared fixture
        }

        protected abstract TContext CreateReadDbContext(DbContextOptions<TContext> options, IMultiTenantContextAccessor<TenantDetails> tenantAccessor);

        /// <summary>
        /// Optional: Override to seed data before each test.
        /// </summary>
        protected virtual Task SeedAsync() => Task.CompletedTask;
    }
}
