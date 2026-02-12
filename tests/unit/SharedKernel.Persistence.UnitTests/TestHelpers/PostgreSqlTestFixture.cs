using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace SharedKernel.Persistence.UnitTests.TestHelpers;

/// <summary>
/// Provides a PostgreSQL test container for database tests.
/// Uses xUnit's IAsyncLifetime for proper async initialization and cleanup.
/// </summary>
/// <typeparam name="TContext">The DbContext type to test</typeparam>
internal sealed class PostgreSqlTestFixture<TContext> : IAsyncLifetime
    where TContext : DbContext
{
    private readonly PostgreSqlContainer _container;

    public PostgreSqlTestFixture()
    {
        _container = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCleanUp(true)
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public DbContextOptions<TContext> CreateDbContextOptions()
    {
        return new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(ConnectionString)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;
    }

    public ValueTask InitializeAsync()
    {
        return new ValueTask(_container.StartAsync());
    }

    public ValueTask DisposeAsync()
    {
        return _container.DisposeAsync();
    }
}
