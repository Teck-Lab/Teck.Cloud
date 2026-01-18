#nullable enable
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Core.Database;
using SharedKernel.Persistence.Database.EFCore.Interceptors;
using Wolverine;
using Wolverine.RabbitMQ;

namespace Catalog.IntegrationTests.Shared
{
    /// <summary>
    /// Base test fixture for testing write repositories
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

        protected BaseWriteRepoTestFixture(SharedTestcontainersFixture sharedFixture)
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
            WriteDbContext = CreateWriteDbContext(options);
            UnitOfWork = CreateUnitOfWork(WriteDbContext);

            // For ephemeral testcontainers we recreate the database so schema matches the current model.
            await WriteDbContext.Database.EnsureDeletedAsync();
            await WriteDbContext.Database.EnsureCreatedAsync();

            await SeedAsync();
        }

        public virtual async ValueTask DisposeAsync()
        {
            // Delete the test database to keep containers clean (containers themselves are disposed by the shared fixture)
            try
            {
                if (WriteDbContext != null)
                {
                    await WriteDbContext.Database.EnsureDeletedAsync();
                    await WriteDbContext.DisposeAsync();
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

        protected abstract TContext CreateWriteDbContext(DbContextOptions<TContext> options);

        protected abstract TUnitOfWork CreateUnitOfWork(TContext context);

        /// <summary>
        /// Optional: Override to seed data before each test.
        /// </summary>
        protected virtual Task SeedAsync() => Task.CompletedTask;
    }
}
