using Customer.Application.Tenants.ReadModels;
using Customer.Infrastructure.Persistence;
using Customer.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Customer.UnitTests.Infrastructure.Persistence;

public sealed class CustomerReadDbContextModelTests : IDisposable
{
    private readonly CustomerReadDbContext _dbContext;

    public CustomerReadDbContextModelTests()
    {
        var options = new DbContextOptionsBuilder<CustomerReadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new CustomerReadDbContext(options);
    }

    [Fact]
    public void Model_ShouldConfigureTenantReadModel()
    {
        // Act
        var entity = _dbContext.Model.FindEntityType(typeof(TenantReadModel));

        // Assert
        entity.ShouldNotBeNull();
        entity.GetTableName().ShouldBe("Tenants");
        entity.FindProperty(nameof(TenantReadModel.Identifier))!.GetMaxLength().ShouldBe(100);
        entity.FindProperty(nameof(TenantReadModel.Name))!.GetMaxLength().ShouldBe(200);
        entity.FindProperty(nameof(TenantReadModel.Plan))!.GetMaxLength().ShouldBe(50);
        entity.FindProperty(nameof(TenantReadModel.KeycloakOrganizationId))!.GetMaxLength().ShouldBe(64);
        entity.FindProperty(nameof(TenantReadModel.DatabaseStrategy))!.GetMaxLength().ShouldBe(50);
        entity.FindProperty(nameof(TenantReadModel.DatabaseProvider))!.GetMaxLength().ShouldBe(50);
        entity.FindProperty(nameof(TenantReadModel.IsActive))!.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Model_ShouldConfigureTenantDatabaseMetadataReadModel()
    {
        // Act
        var entity = _dbContext.Model.FindEntityType(typeof(TenantDatabaseMetadataReadModel));

        // Assert
        entity.ShouldNotBeNull();
        entity.GetTableName().ShouldBe("TenantDatabaseMetadata");

        var primaryKey = entity.FindPrimaryKey();
        primaryKey.ShouldNotBeNull();
        primaryKey.Properties.Count.ShouldBe(2);
        primaryKey.Properties.Select(property => property.Name).ShouldContain(nameof(TenantDatabaseMetadataReadModel.TenantId));
        primaryKey.Properties.Select(property => property.Name).ShouldContain(nameof(TenantDatabaseMetadataReadModel.ServiceName));

        entity.FindProperty(nameof(TenantDatabaseMetadataReadModel.ServiceName))!.GetMaxLength().ShouldBe(100);
        entity.FindProperty(nameof(TenantDatabaseMetadataReadModel.ReadDatabaseMode))!.IsNullable.ShouldBeFalse();
        entity.FindProperty(nameof(TenantDatabaseMetadataReadModel.IsDeleted))!.IsNullable.ShouldBeFalse();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
