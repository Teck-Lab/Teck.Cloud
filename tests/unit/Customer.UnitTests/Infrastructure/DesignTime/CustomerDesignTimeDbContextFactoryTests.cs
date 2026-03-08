using Customer.Infrastructure.DesignTime;
using Shouldly;

namespace Customer.UnitTests.Infrastructure.DesignTime;

public sealed class CustomerDesignTimeDbContextFactoryTests : IDisposable
{
    private readonly string? _originalConnectionString;
    private readonly string? _originalServerType;

    public CustomerDesignTimeDbContextFactoryTests()
    {
        _originalConnectionString = Environment.GetEnvironmentVariable("MIGRATION_CONNECTION_STRING");
        _originalServerType = Environment.GetEnvironmentVariable("MIGRATION_SERVER_TYPE");
    }

    [Fact]
    public void CreateDbContext_ShouldUsePostgresProvider_WhenServerTypeIsPostgres()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MIGRATION_CONNECTION_STRING", "Host=localhost;Database=test;Username=postgres;Password=postgres");
        Environment.SetEnvironmentVariable("MIGRATION_SERVER_TYPE", "POSTGRES");
        var factory = new CustomerDesignTimeDbContextFactory();

        // Act
        using var dbContext = factory.CreateDbContext([]);

        // Assert
        dbContext.Database.ProviderName.ShouldBe("Npgsql.EntityFrameworkCore.PostgreSQL");
    }

    [Fact]
    public void CreateDbContext_ShouldInferSqlServerProvider_FromConnectionString()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MIGRATION_CONNECTION_STRING", "Data Source=.;Initial Catalog=testdb;Trusted_Connection=True");
        Environment.SetEnvironmentVariable("MIGRATION_SERVER_TYPE", null);
        var factory = new CustomerDesignTimeDbContextFactory();

        // Act
        using var dbContext = factory.CreateDbContext([]);

        // Assert
        dbContext.Database.ProviderName.ShouldBe("Microsoft.EntityFrameworkCore.SqlServer");
    }

    [Fact]
    public void CreateDbContext_ShouldFallbackToMySqlProvider_WhenServerTypeUnknown()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MIGRATION_CONNECTION_STRING", "Server=localhost;Database=test;Uid=root;Pwd=root");
        Environment.SetEnvironmentVariable("MIGRATION_SERVER_TYPE", "UNKNOWN");
        var factory = new CustomerDesignTimeDbContextFactory();

        // Act
        using var dbContext = factory.CreateDbContext([]);

        // Assert
        dbContext.Database.ProviderName.ShouldBe("MySql.EntityFrameworkCore");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("MIGRATION_CONNECTION_STRING", _originalConnectionString);
        Environment.SetEnvironmentVariable("MIGRATION_SERVER_TYPE", _originalServerType);
    }
}
