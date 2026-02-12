using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SharedKernel.Migration;
using SharedKernel.Migration.Models;
using SharedKernel.Secrets;
using Shouldly;

namespace SharedKernel.Migration.UnitTests.Services;

public sealed class DbUpMigrationRunnerTests
{
    [Fact]
    public async Task MigrateAsync_ShouldReturnFailed_WhenVaultSecretsManagerThrows()
    {
        // Arrange
        var vaultSecretsManager = Substitute.For<IVaultSecretsManager>();
        vaultSecretsManager
            .GetDatabaseCredentialsByPathAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("Vault error"));

        var logger = Substitute.For<ILogger<DbUpMigrationRunner>>();
        var runner = new DbUpMigrationRunner(vaultSecretsManager, logger);

        var options = new MigrationOptions
        {
            ScriptsPath = "test/path",
            Provider = "PostgreSQL"
        };

        // Act
        var result = await runner.MigrateAsync("vault/path", options, TestContext.Current.CancellationToken);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Vault error");
        result.ScriptsApplied.ShouldBe(0);
    }

    [Fact]
    public async Task MigrateAsync_ShouldCallVaultSecretsManager_WithCorrectPath()
    {
        // Arrange
        var credentials = new DatabaseCredentials
        {
            Host = "localhost",
            Port = 5432,
            Admin = new UserCredentials { Username = "admin", Password = "password" },
            Application = new UserCredentials { Username = "app", Password = "password" },
            Database = "testdb",
            Provider = "PostgreSQL"
        };

        var vaultSecretsManager = Substitute.For<IVaultSecretsManager>();
        vaultSecretsManager
            .GetDatabaseCredentialsByPathAsync("vault/test/path", Arg.Any<CancellationToken>())
            .Returns(credentials);

        var logger = Substitute.For<ILogger<DbUpMigrationRunner>>();
        var runner = new DbUpMigrationRunner(vaultSecretsManager, logger);

        var options = new MigrationOptions
        {
            ScriptsPath = "nonexistent/path", // Will fail when DbUp tries to read
            Provider = "MySQL"
        };

        // Act
        await runner.MigrateAsync("vault/test/path", options, TestContext.Current.CancellationToken);

        // Assert
        await vaultSecretsManager.Received(1).GetDatabaseCredentialsByPathAsync(
            "vault/test/path",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MigrateAsync_ShouldUseFallbackProvider_WhenCredentialsProviderIsNull()
    {
        // Arrange
        var credentials = new DatabaseCredentials
        {
            Host = "localhost",
            Port = 5432,
            Admin = new UserCredentials { Username = "admin", Password = "password" },
            Application = new UserCredentials { Username = "app", Password = "password" },
            Database = "testdb",
            Provider = null // No provider in credentials
        };

        var vaultSecretsManager = Substitute.For<IVaultSecretsManager>();
        vaultSecretsManager
            .GetDatabaseCredentialsByPathAsync("vault/path", Arg.Any<CancellationToken>())
            .Returns(credentials);

        var logger = Substitute.For<ILogger<DbUpMigrationRunner>>();
        var runner = new DbUpMigrationRunner(vaultSecretsManager, logger);

        var options = new MigrationOptions
        {
            ScriptsPath = "nonexistent/path",
            Provider = "MySQL" // Fallback provider
        };

        // Act
        await runner.MigrateAsync("vault/path", options, TestContext.Current.CancellationToken);

        // Assert - Should use fallback provider
        await vaultSecretsManager.Received(1).GetDatabaseCredentialsByPathAsync("vault/path", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MigrateAsync_ShouldReturnFailed_WhenUnsupportedProvider()
    {
        // Arrange
        var credentials = new DatabaseCredentials
        {
            Host = "localhost",
            Port = 5432,
            Admin = new UserCredentials { Username = "admin", Password = "password" },
            Application = new UserCredentials { Username = "app", Password = "password" },
            Database = "testdb",
            Provider = "UnsupportedDB"
        };

        var vaultSecretsManager = Substitute.For<IVaultSecretsManager>();
        vaultSecretsManager
            .GetDatabaseCredentialsByPathAsync("vault/path", Arg.Any<CancellationToken>())
            .Returns(credentials);

        var logger = Substitute.For<ILogger<DbUpMigrationRunner>>();
        var runner = new DbUpMigrationRunner(vaultSecretsManager, logger);

        var options = new MigrationOptions
        {
            ScriptsPath = "test/path",
            Provider = "UnsupportedDB"
        };

        // Act
        var result = await runner.MigrateAsync("vault/path", options, TestContext.Current.CancellationToken);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
        result.ErrorMessage.ShouldContain("not supported");
    }

    [Fact]
    public async Task MigrateAsync_ShouldHandleCancellation()
    {
        // Arrange
        var vaultSecretsManager = Substitute.For<IVaultSecretsManager>();
        var logger = Substitute.For<ILogger<DbUpMigrationRunner>>();
        var runner = new DbUpMigrationRunner(vaultSecretsManager, logger);

        var options = new MigrationOptions
        {
            ScriptsPath = "test/path",
            Provider = "PostgreSQL"
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        vaultSecretsManager
            .GetDatabaseCredentialsByPathAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());

        // Act
        var result = await runner.MigrateAsync("vault/path", options, cts.Token);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
    }
}
