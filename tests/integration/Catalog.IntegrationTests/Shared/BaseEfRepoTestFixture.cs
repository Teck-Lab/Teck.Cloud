using SharedKernel.Persistence.Database.EFCore.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.RabbitMQ;

namespace Catalog.IntegrationTests.Shared
{
    internal abstract class BaseEfRepoTestFixture<TContext, TUnitOfWork> : IAsyncLifetime where TContext : DbContext
    {
        protected readonly SharedTestcontainersFixture SharedFixture;
        protected TContext DbContext = null!;
        protected TUnitOfWork UnitOfWork = default!;
        protected SoftDeleteInterceptor SoftDeleteInterceptor = null!;
        protected AuditingInterceptor AuditingInterceptor = null!;
        protected IServiceProvider ServiceProvider = null!;

        protected BaseEfRepoTestFixture(SharedTestcontainersFixture sharedFixture)
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
            UnitOfWork = CreateUnitOfWork(DbContext);
            await DbContext.Database.EnsureCreatedAsync();
            await SeedAsync();
        }

        public virtual async ValueTask DisposeAsync()
        {
            // Do NOT dispose containers here; handled by shared fixture
        }

        protected abstract TContext CreateDbContext(DbContextOptions<TContext> options);

        protected abstract TUnitOfWork CreateUnitOfWork(TContext context);

        /// <summary>
        /// Optional: Override to seed data before each test.
        /// </summary>
        protected virtual Task SeedAsync() => Task.CompletedTask;
    }
}
