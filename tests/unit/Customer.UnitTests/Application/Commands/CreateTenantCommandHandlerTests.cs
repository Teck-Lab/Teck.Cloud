using Customer.Application.Common.Interfaces;
using Customer.Application.Tenants.Commands.CreateTenant;
using Customer.Application.Tenants.DTOs;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Pricing;
using SharedKernel.Secrets;
using Shouldly;

namespace Customer.UnitTests.Application.Commands;

public class CreateTenantCommandHandlerTests
{
    private readonly ITenantWriteRepository _tenantRepository;
    private readonly IVaultSecretsManager _vaultSecretsManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CreateTenantCommandHandler _sut;

    public CreateTenantCommandHandlerTests()
    {
        _tenantRepository = Substitute.For<ITenantWriteRepository>();
        _vaultSecretsManager = Substitute.For<IVaultSecretsManager>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _sut = new CreateTenantCommandHandler(_tenantRepository, _vaultSecretsManager, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenValidCommandProvided()
    {
        // Arrange
        var command = new CreateTenantCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL,
            null);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(false);

        _vaultSecretsManager.StoreDatabaseCredentialsByPathAsync(
                Arg.Any<string>(),
                Arg.Any<DatabaseCredentials>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        ErrorOr<TenantDto> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldNotBeNull();
        result.Value.Identifier.ShouldBe(command.Identifier);
        result.Value.Name.ShouldBe(command.Name);
        result.Value.Plan.ShouldBe(command.Plan);

        await _tenantRepository.Received(1).AddAsync(
            Arg.Any<Customer.Domain.Entities.TenantAggregate.Tenant>(),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnConflictError_WhenTenantAlreadyExists()
    {
        // Arrange
        var command = new CreateTenantCommand(
            "existing-tenant",
            "Existing Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL,
            null);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        ErrorOr<TenantDto> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe("Tenant.AlreadyExists");

        await _tenantRepository.DidNotReceive().AddAsync(
            Arg.Any<Customer.Domain.Entities.TenantAggregate.Tenant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldStoreCredentialsInVault_ForEachService()
    {
        // Arrange
        var command = new CreateTenantCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL,
            null);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(false);

        _vaultSecretsManager.StoreDatabaseCredentialsByPathAsync(
                Arg.Any<string>(),
                Arg.Any<DatabaseCredentials>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        ErrorOr<TenantDto> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeFalse();

        // Should store credentials for 3 services (catalog, orders, customer) x 2 (write + read) = 6 total
        await _vaultSecretsManager.Received(6).StoreDatabaseCredentialsByPathAsync(
            Arg.Any<string>(),
            Arg.Any<DatabaseCredentials>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldInitializeMigrationStatus_ForEachService()
    {
        // Arrange
        var command = new CreateTenantCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL,
            null);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(false);

        _vaultSecretsManager.StoreDatabaseCredentialsByPathAsync(
                Arg.Any<string>(),
                Arg.Any<DatabaseCredentials>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        ErrorOr<TenantDto> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.MigrationStatuses.Count.ShouldBe(3); // catalog, orders, customer
        result.Value.MigrationStatuses.ShouldAllBe(ms => ms.Status == SharedKernel.Migration.Models.MigrationStatus.Pending);
    }

    [Fact]
    public async Task Handle_ShouldUseSharedCredentials_WhenStrategyIsShared()
    {
        // Arrange
        var command = new CreateTenantCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Shared,
            DatabaseProvider.PostgreSQL,
            null);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(false);

        // Mock CredentialsExistAsync to return false so credentials get generated
        _vaultSecretsManager.CredentialsExistAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(false);

        _vaultSecretsManager.StoreDatabaseCredentialsByPathAsync(
                Arg.Any<string>(),
                Arg.Any<DatabaseCredentials>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        ErrorOr<TenantDto> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeFalse();

        // For shared strategy, credentials are generated and stored for each service x 2 (write + read) = 6 total
        await _vaultSecretsManager.Received(6).StoreDatabaseCredentialsByPathAsync(
            Arg.Is<string>(path => path.Contains("database/shared/")),
            Arg.Any<DatabaseCredentials>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldUseCustomCredentials_WhenStrategyIsExternal()
    {
        // Arrange
        var customCredentials = new DatabaseCredentials
        {
            Admin = new UserCredentials { Username = "custom_admin", Password = "custom_pass" },
            Application = new UserCredentials { Username = "custom_app", Password = "custom_pass" },
            Host = "custom-postgres",
            Port = 5432,
            Database = "custom_db",
            Provider = "PostgreSQL"
        };

        var command = new CreateTenantCommand(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.External,
            DatabaseProvider.PostgreSQL,
            customCredentials);

        _tenantRepository.ExistsByIdentifierAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns(false);

        _vaultSecretsManager.StoreDatabaseCredentialsByPathAsync(
                Arg.Any<string>(),
                Arg.Any<DatabaseCredentials>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        ErrorOr<TenantDto> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeFalse();

        // For External strategy, should store custom credentials only for write path (3 services)
        // External databases don't have separate read replicas managed by us
        await _vaultSecretsManager.Received(3).StoreDatabaseCredentialsByPathAsync(
            Arg.Is<string>(path => path.Contains("database/tenants/") && path.EndsWith("/write")),
            Arg.Is<DatabaseCredentials>(creds => creds.Host == "custom-postgres"),
            Arg.Any<CancellationToken>());
    }
}
