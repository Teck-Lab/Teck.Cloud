using Customer.Application.Tenants.Queries.CheckServiceReadiness;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Pricing;
using SharedKernel.Migration.Models;
using Shouldly;

namespace Customer.UnitTests.Application.Queries.Tenants;

public sealed class CheckServiceReadinessQueryHandlerTests
{
    private readonly ITenantWriteRepository _tenantRepository;
    private readonly CheckServiceReadinessQueryHandler _handler;

    public CheckServiceReadinessQueryHandlerTests()
    {
        _tenantRepository = Substitute.For<ITenantWriteRepository>();
        _handler = new CheckServiceReadinessQueryHandler(_tenantRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnTrue_WhenMigrationStatusIsCompleted()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceName = "CatalogService";
        
        var tenantResult = Tenant.Create(
            "test-tenant",
            "Test Tenant",
            "Pro",
            DatabaseStrategy.Shared,
            DatabaseProvider.PostgreSQL);

        var tenant = tenantResult.Value;
        tenant.InitializeMigrationStatus(serviceName);
        tenant.UpdateMigrationStatus(
            serviceName,
            MigrationStatus.Completed,
            "20240101_InitialMigration",
            null);

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var query = new CheckServiceReadinessQuery(tenantId, serviceName);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBeTrue();

        await _tenantRepository.Received(1).GetByIdAsync(tenantId, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(MigrationStatus.Pending)]
    [InlineData(MigrationStatus.InProgress)]
    [InlineData(MigrationStatus.Failed)]
    public async Task Handle_ShouldReturnFalse_WhenMigrationStatusIsNotCompleted(MigrationStatus status)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceName = "CatalogService";
        
        var tenantResult = Tenant.Create(
            "test-tenant",
            "Test Tenant",
            "Pro",
            DatabaseStrategy.Shared,
            DatabaseProvider.PostgreSQL);

        var tenant = tenantResult.Value;
        tenant.InitializeMigrationStatus(serviceName);
        tenant.UpdateMigrationStatus(
            serviceName,
            status,
            null,
            status == MigrationStatus.Failed ? "Migration failed" : null);

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var query = new CheckServiceReadinessQuery(tenantId, serviceName);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBeFalse();

        await _tenantRepository.Received(1).GetByIdAsync(tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFoundError_WhenTenantDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceName = "CatalogService";
        
        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        var query = new CheckServiceReadinessQuery(tenantId, serviceName);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Tenant.NotFound");
        result.FirstError.Description.ShouldBe($"Tenant with ID '{tenantId}' not found");

        await _tenantRepository.Received(1).GetByIdAsync(tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFoundError_WhenMigrationStatusForServiceDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceName = "NonExistentService";
        
        var tenantResult = Tenant.Create(
            "test-tenant",
            "Test Tenant",
            "Pro",
            DatabaseStrategy.Shared,
            DatabaseProvider.PostgreSQL);

        var tenant = tenantResult.Value;
        // Initialize migration status for a different service
        tenant.InitializeMigrationStatus("CatalogService");

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var query = new CheckServiceReadinessQuery(tenantId, serviceName);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Tenant.MigrationStatusNotFound");
        result.FirstError.Description.ShouldBe($"Migration status for service '{serviceName}' not found");

        await _tenantRepository.Received(1).GetByIdAsync(tenantId, Arg.Any<CancellationToken>());
    }
}
