using Customer.Application.Tenants.Queries.CheckServiceReadiness;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Pricing;

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
        tenant.AddDatabaseMetadata(
            serviceName,
            "ConnectionStrings__Tenants__test-tenant__Write",
            "ConnectionStrings__Tenants__test-tenant__Read",
            true);

        // Set environment variable for the tenant write DSN
        Environment.SetEnvironmentVariable("ConnectionStrings__Tenants__test-tenant__Write", "Host=localhost;Database=db;Username=user;Password=pass");

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

    [Fact]
    public async Task Handle_ShouldReturnFalse_WhenDsnEnvVarIsMissing()
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
        tenant.AddDatabaseMetadata(
            serviceName,
            "ConnectionStrings__Tenants__test-tenant__Write",
            null,
            false);

        // Ensure no env var is set for write DSN to simulate non-ready state
        Environment.SetEnvironmentVariable("ConnectionStrings__Tenants__test-tenant__Write", null);

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
    public async Task Handle_ShouldReturnNotFoundError_WhenDatabaseMetadataForServiceDoesNotExist()
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
        // Add metadata for a different service
        tenant.AddDatabaseMetadata(
            "CatalogService",
            "ConnectionStrings__Tenants__test-tenant__Write",
            null,
            false);

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var query = new CheckServiceReadinessQuery(tenantId, serviceName);


        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Tenant.DatabaseMetadataNotFound");
        result.FirstError.Description.ShouldBe($"Database metadata for service '{serviceName}' not found");

        await _tenantRepository.Received(1).GetByIdAsync(tenantId, Arg.Any<CancellationToken>());
    }

}
