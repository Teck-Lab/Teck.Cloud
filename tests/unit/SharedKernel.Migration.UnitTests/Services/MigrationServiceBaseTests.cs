using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Migration;
using SharedKernel.Migration.Services;
using SharedKernel.Secrets;
using Shouldly;

namespace SharedKernel.Migration.UnitTests.Services;

public sealed class MigrationServiceBaseTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenServiceNameIsNull()
    {
        // Arrange
        var vaultSecretsManager = Substitute.For<IVaultSecretsManager>();
        var logger1 = Substitute.For<ILogger<DbUpMigrationRunner>>();
        var migrationRunner = new DbUpMigrationRunner(vaultSecretsManager, logger1);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var logger2 = Substitute.For<ILogger<CustomerApiClient>>();
        var customerApiClient = new CustomerApiClient(httpClientFactory, logger2);
        var logger3 = Substitute.For<ILogger<TestMigrationService>>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new TestMigrationService(null!, vaultSecretsManager, migrationRunner, customerApiClient, logger3));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenVaultSecretsManagerIsNull()
    {
        // Arrange
        var vaultSecretsManager = Substitute.For<IVaultSecretsManager>();
        var logger1 = Substitute.For<ILogger<DbUpMigrationRunner>>();
        var migrationRunner = new DbUpMigrationRunner(vaultSecretsManager, logger1);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var logger2 = Substitute.For<ILogger<CustomerApiClient>>();
        var customerApiClient = new CustomerApiClient(httpClientFactory, logger2);
        var logger3 = Substitute.For<ILogger<TestMigrationService>>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new TestMigrationService("test", null!, migrationRunner, customerApiClient, logger3));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenMigrationRunnerIsNull()
    {
        // Arrange
        var vaultSecretsManager = Substitute.For<IVaultSecretsManager>();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var logger2 = Substitute.For<ILogger<CustomerApiClient>>();
        var customerApiClient = new CustomerApiClient(httpClientFactory, logger2);
        var logger3 = Substitute.For<ILogger<TestMigrationService>>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new TestMigrationService("test", vaultSecretsManager, null!, customerApiClient, logger3));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCustomerApiClientIsNull()
    {
        // Arrange
        var vaultSecretsManager = Substitute.For<IVaultSecretsManager>();
        var logger1 = Substitute.For<ILogger<DbUpMigrationRunner>>();
        var migrationRunner = new DbUpMigrationRunner(vaultSecretsManager, logger1);
        var logger3 = Substitute.For<ILogger<TestMigrationService>>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new TestMigrationService("test", vaultSecretsManager, migrationRunner, null!, logger3));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLoggerIsNull()
    {
        // Arrange
        var vaultSecretsManager = Substitute.For<IVaultSecretsManager>();
        var logger1 = Substitute.For<ILogger<DbUpMigrationRunner>>();
        var migrationRunner = new DbUpMigrationRunner(vaultSecretsManager, logger1);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var logger2 = Substitute.For<ILogger<CustomerApiClient>>();
        var customerApiClient = new CustomerApiClient(httpClientFactory, logger2);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new TestMigrationService("test", vaultSecretsManager, migrationRunner, customerApiClient, null!));
    }

    public sealed class TestMigrationService : MigrationServiceBase
    {
        public TestMigrationService(
            string serviceName,
            IVaultSecretsManager vaultSecretsManager,
            DbUpMigrationRunner migrationRunner,
            CustomerApiClient customerApiClient,
            ILogger<TestMigrationService> logger)
            : base(serviceName, vaultSecretsManager, migrationRunner, customerApiClient, logger)
        {
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
            Task.CompletedTask;
    }
}
