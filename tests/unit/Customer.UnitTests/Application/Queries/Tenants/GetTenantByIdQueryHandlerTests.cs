using Customer.Application.Tenants.Queries.GetTenantById;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Pricing;
using Shouldly;

namespace Customer.UnitTests.Application.Queries.Tenants;

public sealed class GetTenantByIdQueryHandlerTests
{
    private readonly ITenantWriteRepository _tenantRepository;
    private readonly GetTenantByIdQueryHandler _handler;

    public GetTenantByIdQueryHandlerTests()
    {
        _tenantRepository = Substitute.For<ITenantWriteRepository>();
        _handler = new GetTenantByIdQueryHandler(_tenantRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnTenantDto_WhenTenantExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenantResult = Tenant.Create(
            "test-tenant",
            "Test Tenant",
            "Pro",
            DatabaseStrategy.Shared,
            DatabaseProvider.PostgreSQL);

        tenantResult.IsError.ShouldBeFalse();
        var tenant = tenantResult.Value;

        // Add database metadata
        tenant.AddDatabaseMetadata(
            "CatalogService",
            "secret/data/tenants/test-tenant/catalog/write",
            "secret/data/tenants/test-tenant/catalog/read",
            true);

        // Initialize and update migration status
        tenant.InitializeMigrationStatus("CatalogService");
        tenant.UpdateMigrationStatus(
            "CatalogService",
            SharedKernel.Migration.Models.MigrationStatus.Completed,
            "20240101_InitialMigration",
            null);

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var query = new GetTenantByIdQuery(tenantId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        var dto = result.Value;
        
        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(tenant.Id);
        dto.Identifier.ShouldBe("test-tenant");
        dto.Name.ShouldBe("Test Tenant");
        dto.Plan.ShouldBe("Pro");
        dto.DatabaseStrategy.ShouldBe("Shared");
        dto.DatabaseProvider.ShouldBe("PostgreSQL");
        dto.IsActive.ShouldBeTrue();
        
        dto.Databases.Count.ShouldBe(1);
        var database = dto.Databases.First();
        database.ServiceName.ShouldBe("CatalogService");
        database.VaultWritePath.ShouldBe("secret/data/tenants/test-tenant/catalog/write");
        database.VaultReadPath.ShouldBe("secret/data/tenants/test-tenant/catalog/read");
        database.HasSeparateReadDatabase.ShouldBeTrue();
        
        dto.MigrationStatuses.Count.ShouldBe(1);
        var migrationStatus = dto.MigrationStatuses.First();
        migrationStatus.ServiceName.ShouldBe("CatalogService");
        migrationStatus.Status.ShouldBe(SharedKernel.Migration.Models.MigrationStatus.Completed);
        migrationStatus.LastMigrationVersion.ShouldBe("20240101_InitialMigration");
        migrationStatus.ErrorMessage.ShouldBeNull();

        await _tenantRepository.Received(1).GetByIdAsync(tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenTenantNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        var query = new GetTenantByIdQuery(tenantId);

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
    public async Task Handle_ShouldMapMultipleDatabases_WhenTenantHasMultipleServices()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenantResult = Tenant.Create(
            "multi-service-tenant",
            "Multi Service Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL);

        var tenant = tenantResult.Value;

        // Add multiple database metadata
        tenant.AddDatabaseMetadata(
            "CatalogService",
            "secret/data/tenants/multi/catalog/write",
            null,
            false);

        tenant.AddDatabaseMetadata(
            "CustomerService",
            "secret/data/tenants/multi/customer/write",
            "secret/data/tenants/multi/customer/read",
            true);

        // Initialize migration statuses
        tenant.InitializeMigrationStatus("CatalogService");
        tenant.InitializeMigrationStatus("CustomerService");

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var query = new GetTenantByIdQuery(tenantId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        var dto = result.Value;
        
        dto.Databases.Count.ShouldBe(2);
        dto.Databases.ShouldContain(db => db.ServiceName == "CatalogService");
        dto.Databases.ShouldContain(db => db.ServiceName == "CustomerService");
        
        dto.MigrationStatuses.Count.ShouldBe(2);
        dto.MigrationStatuses.ShouldContain(ms => ms.ServiceName == "CatalogService");
        dto.MigrationStatuses.ShouldContain(ms => ms.ServiceName == "CustomerService");
    }
}
