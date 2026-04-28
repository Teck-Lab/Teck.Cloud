using Customer.Application.Tenants.ReadModels;
using Customer.Infrastructure.Persistence;
using Customer.Infrastructure.Persistence.ReadModels;
using Customer.Infrastructure.Persistence.Repositories.Read;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Customer.UnitTests.Infrastructure.Persistence.Repositories;

public sealed class TenantReadRepositoryTests : IDisposable
{
    private readonly CustomerReadDbContext _dbContext;
    private readonly TenantReadRepository _repository;

    public TenantReadRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<CustomerReadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new CustomerReadDbContext(options);
        _repository = new TenantReadRepository(_dbContext);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnTenant_WhenKeycloakOrganizationIdMatches()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var tenant = new TenantReadModel
        {
            Id = Guid.NewGuid(),
            Identifier = "tenant-one",
            Name = "Tenant One",
            Plan = "Enterprise",
            KeycloakOrganizationId = organizationId.ToString(),
            DatabaseStrategy = "Dedicated",
            DatabaseProvider = "PostgreSQL",
            IsActive = true,
        };

        await _dbContext.Tenants.AddAsync(tenant, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByIdAsync(organizationId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(tenant.Id);
        result.Identifier.ShouldBe("tenant-one");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenTenantIsMissing()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetDatabaseInfoByIdAsync_ShouldReturnNull_WhenTenantIsMissing()
    {
        // Act
        var result = await _repository.GetDatabaseInfoByIdAsync(Guid.NewGuid(), "catalog", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetDatabaseInfoByIdAsync_ShouldResolveServiceName_CaseInsensitiveAndTrimmed()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var tenant = new TenantReadModel
        {
            Id = Guid.NewGuid(),
            Identifier = "tenant-two",
            Name = "Tenant Two",
            Plan = "Starter",
            KeycloakOrganizationId = organizationId.ToString(),
            DatabaseStrategy = "Shared",
            DatabaseProvider = "PostgreSQL",
            IsActive = true,
        };

        var metadataRows = new[]
        {
            new TenantDatabaseMetadataReadModel
            {
                TenantId = tenant.Id,
                ServiceName = "catalog",
                ReadDatabaseMode = 1,
                IsDeleted = false,
            },
            new TenantDatabaseMetadataReadModel
            {
                TenantId = tenant.Id,
                ServiceName = "customer",
                ReadDatabaseMode = 0,
                IsDeleted = false,
            },
        };

        await _dbContext.Tenants.AddAsync(tenant, TestContext.Current.CancellationToken);
        await _dbContext.TenantDatabaseMetadata.AddRangeAsync(metadataRows, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetDatabaseInfoByIdAsync(organizationId, "  CATALOG  ", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.TenantId.ShouldBe(tenant.Id);
        result.DatabaseStrategy.ShouldBe("Shared");
        result.DatabaseProvider.ShouldBe("PostgreSQL");
        result.HasReadReplicas.ShouldBeTrue();
    }

    [Fact]
    public async Task GetDatabaseInfoByIdAsync_ShouldUseFirstOrderedMetadata_WhenServiceNameMissing()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var tenant = new TenantReadModel
        {
            Id = Guid.NewGuid(),
            Identifier = "tenant-three",
            Name = "Tenant Three",
            Plan = "Enterprise",
            KeycloakOrganizationId = organizationId.ToString(),
            DatabaseStrategy = "Dedicated",
            DatabaseProvider = "MySql",
            IsActive = true,
        };

        var metadataRows = new[]
        {
            new TenantDatabaseMetadataReadModel
            {
                TenantId = tenant.Id,
                ServiceName = "zeta",
                ReadDatabaseMode = 1,
                IsDeleted = false,
            },
            new TenantDatabaseMetadataReadModel
            {
                TenantId = tenant.Id,
                ServiceName = "alpha",
                ReadDatabaseMode = 0,
                IsDeleted = false,
            },
        };

        await _dbContext.Tenants.AddAsync(tenant, TestContext.Current.CancellationToken);
        await _dbContext.TenantDatabaseMetadata.AddRangeAsync(metadataRows, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetDatabaseInfoByIdAsync(organizationId, null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.HasReadReplicas.ShouldBeFalse();
    }

    [Fact]
    public async Task ListConnectionSeedsAsync_ShouldReturnOnlyActiveTenants()
    {
        // Arrange
        TenantReadModel activeTenant = new()
        {
            Id = Guid.NewGuid(),
            Identifier = "tenant-active",
            Name = "Tenant Active",
            Plan = "Starter",
            DatabaseStrategy = "Shared",
            DatabaseProvider = "PostgreSQL",
            IsActive = true,
        };

        TenantReadModel inactiveTenant = new()
        {
            Id = Guid.NewGuid(),
            Identifier = "tenant-inactive",
            Name = "Tenant Inactive",
            Plan = "Starter",
            DatabaseStrategy = "Dedicated",
            DatabaseProvider = "PostgreSQL",
            IsActive = false,
        };

        await _dbContext.Tenants.AddRangeAsync([activeTenant, inactiveTenant], TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        IReadOnlyList<TenantConnectionSeedReadModel> result = await _repository
            .ListConnectionSeedsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(1);
        result[0].TenantId.ShouldBe(activeTenant.Id);
        result[0].Identifier.ShouldBe("tenant-active");
        result[0].DatabaseStrategy.ShouldBe("Shared");
    }

    [Fact]
    public async Task GetPagedTenantsAsync_ShouldReturnPagedResults_WithPlanAndActiveFilters()
    {
        // Arrange
        TenantReadModel matchingTenant = new()
        {
            Id = Guid.NewGuid(),
            Identifier = "tenant-matching",
            Name = "Tenant Matching",
            Plan = "Business",
            DatabaseStrategy = "Dedicated",
            DatabaseProvider = "PostgreSQL",
            IsActive = true,
        };

        TenantReadModel differentPlanTenant = new()
        {
            Id = Guid.NewGuid(),
            Identifier = "tenant-plan-miss",
            Name = "Tenant Plan Miss",
            Plan = "Starter",
            DatabaseStrategy = "Shared",
            DatabaseProvider = "PostgreSQL",
            IsActive = true,
        };

        TenantReadModel inactiveTenant = new()
        {
            Id = Guid.NewGuid(),
            Identifier = "tenant-inactive",
            Name = "Tenant Inactive",
            Plan = "Business",
            DatabaseStrategy = "Dedicated",
            DatabaseProvider = "PostgreSQL",
            IsActive = false,
        };

        await _dbContext.Tenants.AddRangeAsync([matchingTenant, differentPlanTenant, inactiveTenant], TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetPagedTenantsAsync(
            page: 1,
            size: 10,
            keyword: null,
            plan: "Business",
            isActive: true,
            TestContext.Current.CancellationToken);

        // Assert
        result.TotalItems.ShouldBe(1);
        result.Items.Count.ShouldBe(1);
        result.Items[0].Identifier.ShouldBe("tenant-matching");
        result.Items[0].Plan.ShouldBe("Business");
        result.Items[0].IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetPagedTenantsAsync_ShouldFilterByKeywordAgainstIdentifierAndName()
    {
        // Arrange
        TenantReadModel firstTenant = new()
        {
            Id = Guid.NewGuid(),
            Identifier = "northwind",
            Name = "Northwind Traders",
            Plan = "Business",
            DatabaseStrategy = "Dedicated",
            DatabaseProvider = "PostgreSQL",
            IsActive = true,
        };

        TenantReadModel secondTenant = new()
        {
            Id = Guid.NewGuid(),
            Identifier = "contoso",
            Name = "Contoso Ltd",
            Plan = "Starter",
            DatabaseStrategy = "Shared",
            DatabaseProvider = "PostgreSQL",
            IsActive = true,
        };

        await _dbContext.Tenants.AddRangeAsync([firstTenant, secondTenant], TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var byIdentifier = await _repository.GetPagedTenantsAsync(
            page: 1,
            size: 10,
            keyword: "north",
            plan: null,
            isActive: null,
            TestContext.Current.CancellationToken);

        var byName = await _repository.GetPagedTenantsAsync(
            page: 1,
            size: 10,
            keyword: "Contoso",
            plan: null,
            isActive: null,
            TestContext.Current.CancellationToken);

        // Assert
        byIdentifier.TotalItems.ShouldBe(1);
        byIdentifier.Items[0].Identifier.ShouldBe("northwind");

        byName.TotalItems.ShouldBe(1);
        byName.Items[0].Identifier.ShouldBe("contoso");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
