using Customer.Application.Tenants.Queries.GetTenantDatabaseInfo;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Pricing;
using Shouldly;

namespace Customer.UnitTests.Application.Queries.Tenants;

public sealed class GetTenantDatabaseInfoQueryHandlerTests
{
    private readonly ITenantWriteRepository _tenantRepository;
    private readonly GetTenantDatabaseInfoQueryHandler _handler;

    public GetTenantDatabaseInfoQueryHandlerTests()
    {
        _tenantRepository = Substitute.For<ITenantWriteRepository>();
        _handler = new GetTenantDatabaseInfoQueryHandler(_tenantRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnServiceDatabaseInfoDto_WhenDatabaseExists()
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

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var query = new GetTenantDatabaseInfoQuery(tenantId, serviceName);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        var dto = result.Value;
        
        dto.ShouldNotBeNull();
        dto.WriteEnvVarKey.ShouldBe("ConnectionStrings__Tenants__test-tenant__Write");
        dto.ReadEnvVarKey.ShouldBe("ConnectionStrings__Tenants__test-tenant__Read");
        dto.HasSeparateReadDatabase.ShouldBeTrue();

        await _tenantRepository.Received(1).GetByIdAsync(tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnServiceDatabaseInfoDto_WhenDatabaseHasNoReadReplica()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceName = "CatalogService";
        
        var tenantResult = Tenant.Create(
            "test-tenant",
            "Test Tenant",
            "Free",
            DatabaseStrategy.Shared,
            DatabaseProvider.PostgreSQL);

        var tenant = tenantResult.Value;
        tenant.AddDatabaseMetadata(
            serviceName,
            "secret/data/tenants/test-tenant/catalog/write",
            null,
            false);

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var query = new GetTenantDatabaseInfoQuery(tenantId, serviceName);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        var dto = result.Value;
        
        dto.ShouldNotBeNull();
        dto.WriteEnvVarKey.ShouldBe("ConnectionStrings__Tenants__test-tenant__Write");
        dto.ReadEnvVarKey.ShouldBeNull();
        dto.HasSeparateReadDatabase.ShouldBeFalse();

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

        var query = new GetTenantDatabaseInfoQuery(tenantId, serviceName);

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
        // Add database metadata for a different service
        tenant.AddDatabaseMetadata(
            "CatalogService",
            "secret/data/tenants/test-tenant/catalog/write",
            null,
            false);

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var query = new GetTenantDatabaseInfoQuery(tenantId, serviceName);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Tenant.DatabaseNotFound");
        result.FirstError.Description.ShouldBe($"Database metadata for service '{serviceName}' not found");

        await _tenantRepository.Received(1).GetByIdAsync(tenantId, Arg.Any<CancellationToken>());
    }
}
