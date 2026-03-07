using Microsoft.Extensions.Configuration;
using SharedKernel.Core.Pricing;
using SharedKernel.Persistence.Database;
using Shouldly;

namespace SharedKernel.Persistence.UnitTests.Database.Extensions;

public sealed class DatabaseProviderConfigurationTests
{
    [Fact]
    public void GetDatabaseProvider_ShouldDefaultToPostgreSql_WhenDatabaseProviderMissing()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        DatabaseProvider provider = configuration.GetDatabaseProvider();

        // Assert
        provider.ShouldBe(DatabaseProvider.PostgreSQL);
    }

    [Theory]
    [InlineData("PostgreSQL", nameof(DatabaseProvider.PostgreSQL))]
    [InlineData("postgres", nameof(DatabaseProvider.PostgreSQL))]
    [InlineData("SqlServer", nameof(DatabaseProvider.SqlServer))]
    [InlineData("mssql", nameof(DatabaseProvider.SqlServer))]
    [InlineData("MySQL", nameof(DatabaseProvider.MySQL))]
    public void GetDatabaseProvider_ShouldReadDatabaseProviderFromDatabaseSection(string configuredProvider, string expectedProviderName)
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = configuredProvider,
            })
            .Build();

        // Act
        DatabaseProvider provider = configuration.GetDatabaseProvider();

        // Assert
        provider.Name.ShouldBe(expectedProviderName);
    }

    [Fact]
    public void GetDatabaseProvider_ShouldUseEnvironmentStyleDatabaseProvider_WhenDatabaseSectionMissing()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database__Provider"] = "SqlServer",
            })
            .Build();

        // Act
        DatabaseProvider provider = configuration.GetDatabaseProvider();

        // Assert
        provider.ShouldBe(DatabaseProvider.SqlServer);
    }
}
