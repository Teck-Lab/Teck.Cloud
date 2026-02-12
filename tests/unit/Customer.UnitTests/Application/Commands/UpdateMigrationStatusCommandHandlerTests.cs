using Customer.Application.Common.Interfaces;
using Customer.Application.Tenants.Commands.UpdateMigrationStatus;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Pricing;
using SharedKernel.Migration.Models;
using Shouldly;

namespace Customer.UnitTests.Application.Commands;

public class UpdateMigrationStatusCommandHandlerTests
{
    private readonly ITenantWriteRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateMigrationStatusCommandHandler _sut;

    public UpdateMigrationStatusCommandHandlerTests()
    {
        _tenantRepository = Substitute.For<ITenantWriteRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _sut = new UpdateMigrationStatusCommandHandler(_tenantRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenValidCommandProvided()
    {
        // Arrange
        var tenant = Tenant.Create(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL).Value;
        
        tenant.InitializeMigrationStatus("catalog");

        var command = new UpdateMigrationStatusCommand(
            tenant.Id,
            "catalog",
            MigrationStatus.Completed,
            "0001_InitialMigration",
            null);

        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(tenant);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        ErrorOr<Updated> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeFalse();
        
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldUpdateMigrationStatus_WhenCalled()
    {
        // Arrange
        var tenant = Tenant.Create(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL).Value;
        
        tenant.InitializeMigrationStatus("catalog");

        var command = new UpdateMigrationStatusCommand(
            tenant.Id,
            "catalog",
            MigrationStatus.Completed,
            "0001_InitialMigration",
            null);

        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(tenant);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        var migrationStatus = tenant.MigrationStatuses.First(ms => ms.ServiceName == "catalog");
        migrationStatus.Status.ShouldBe(MigrationStatus.Completed);
        migrationStatus.LastMigrationVersion.ShouldBe("0001_InitialMigration");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFoundError_WhenTenantDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var command = new UpdateMigrationStatusCommand(
            tenantId,
            "catalog",
            MigrationStatus.Completed,
            "0001_InitialMigration",
            null);

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        // Act
        ErrorOr<Updated> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Tenant.NotFound");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldStoreErrorMessage_WhenMigrationFails()
    {
        // Arrange
        var tenant = Tenant.Create(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL).Value;
        
        tenant.InitializeMigrationStatus("catalog");

        var errorMessage = "Migration failed: connection timeout";
        var command = new UpdateMigrationStatusCommand(
            tenant.Id,
            "catalog",
            MigrationStatus.Failed,
            null,
            errorMessage);

        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(tenant);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        var migrationStatus = tenant.MigrationStatuses.First(ms => ms.ServiceName == "catalog");
        migrationStatus.Status.ShouldBe(MigrationStatus.Failed);
        migrationStatus.ErrorMessage.ShouldBe(errorMessage);
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenServiceNotInitialized()
    {
        // Arrange
        var tenant = Tenant.Create(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL).Value;

        // Not initializing migration status for catalog

        var command = new UpdateMigrationStatusCommand(
            tenant.Id,
            "catalog",
            MigrationStatus.Completed,
            "0001_InitialMigration",
            null);

        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Act
        ErrorOr<Updated> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Tenant.MigrationStatusNotFound");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
