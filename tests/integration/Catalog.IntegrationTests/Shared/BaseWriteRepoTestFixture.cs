#nullable enable
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Core.Database;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Persistence.Database.EFCore.Interceptors;

namespace Catalog.IntegrationTests.Shared
{
    /// <summary>
    /// Base test fixture for testing write repositories with shared-database table-truncation isolation.
    /// </summary>
    public abstract class BaseWriteRepoTestFixture<TContext, TUnitOfWork> : IAsyncLifetime
        where TContext : DbContext
        where TUnitOfWork : IUnitOfWork
    {
        protected readonly SharedTestcontainersFixture SharedFixture;
        protected TContext WriteDbContext = null!;
        protected TUnitOfWork UnitOfWork = default!;
        protected SoftDeleteInterceptor SoftDeleteInterceptor = null!;
        protected AuditingInterceptor AuditingInterceptor = null!;
        protected IServiceProvider ServiceProvider = null!;
        protected string? ConnectionString;

        protected BaseWriteRepoTestFixture(SharedTestcontainersFixture sharedFixture)
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
            WriteDbContext = CreateWriteDbContext(options, tenantAccessor);

            UnitOfWork = CreateUnitOfWork(WriteDbContext);

            await SeedAsync();
        }

        public virtual async ValueTask DisposeAsync()
        {
            try
            {
                if (WriteDbContext != null)
                {
                    await WriteDbContext.DisposeAsync();
                }

                if (ConnectionString is not null)
                {
                    await SharedFixture.TruncateAllTablesAsync(ConnectionString, TestContext.Current.CancellationToken);
                }
            }
            catch
            {
                // best-effort cleanup in test teardown — ignore failures
            }

            if (ServiceProvider != null)
            {
                if (ServiceProvider is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (ServiceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            GC.SuppressFinalize(this);
        }

        protected abstract TContext CreateWriteDbContext(DbContextOptions<TContext> options, IMultiTenantContextAccessor<TenantDetails> tenantAccessor);

        protected abstract TUnitOfWork CreateUnitOfWork(TContext context);

        /// <summary>
        /// Optional: Override to seed data before each test.
        /// </summary>
        protected virtual Task SeedAsync() => Task.CompletedTask;
    }
}
