using SharedKernel.Core.Database;
using SharedKernel.Persistence.Database.EFCore;
using SharedKernel.Persistence.Database.EFCore.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.RabbitMQ;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;

namespace Catalog.IntegrationTests.Shared
{
    internal abstract class BaseCacheTestFixture<TContext> : IAsyncLifetime where TContext : BaseDbContext
    {
        protected FusionCache Cache = null!;
        protected IUnitOfWork UnitOfWork = null!;
        protected TContext DbContext = null!;
        protected SoftDeleteInterceptor SoftDeleteInterceptor = null!;
        protected AuditingInterceptor AuditingInterceptor = null!;
        protected IServiceProvider ServiceProvider = null!;
        protected readonly SharedTestcontainersFixture SharedFixture;

        protected BaseCacheTestFixture(SharedTestcontainersFixture sharedFixture)
        {
            SharedFixture = sharedFixture;
        }

        public virtual async ValueTask InitializeAsync()
        {
            // Containers are already started by the shared fixture
            var httpContextAccessor = new HttpContextAccessor();
            var services = new ServiceCollection();
            services.AddSingleton<IHttpContextAccessor>(httpContextAccessor);
            services.AddWolverine(x =>
            {
                x.UseRabbitMq(SharedFixture.RabbitMqContainer.GetConnectionString());
            });
            ServiceProvider = services.BuildServiceProvider();
            SoftDeleteInterceptor = new SoftDeleteInterceptor(httpContextAccessor);
            AuditingInterceptor = new AuditingInterceptor(httpContextAccessor);
            var options = new DbContextOptionsBuilder<TContext>()
                .UseNpgsql(SharedFixture.DbContainer.GetConnectionString())
                .AddInterceptors(SoftDeleteInterceptor, AuditingInterceptor)
                .Options;
            DbContext = CreateDbContext(options);
            await DbContext.Database.EnsureCreatedAsync();
            await SeedAsync();

            var cacheOptions = new FusionCacheOptions
            {
                // You can tweak options here if needed
            };
            Cache = new FusionCache(cacheOptions);

            // Add in-memory backplane for multi-instance cache sync (optional)
            var backplane = new MemoryBackplane(new MemoryBackplaneOptions());
            Cache.SetupBackplane(backplane);
        }

        public virtual async ValueTask DisposeAsync()
        {
            // Do NOT dispose containers here; handled by shared fixture
            Cache?.Dispose();
        }

        protected abstract TContext CreateDbContext(DbContextOptions<TContext> options);

        /// <summary>
        /// Optional: Override to seed data before each test.
        /// </summary>
        protected virtual Task SeedAsync() => Task.CompletedTask;
    }
}
