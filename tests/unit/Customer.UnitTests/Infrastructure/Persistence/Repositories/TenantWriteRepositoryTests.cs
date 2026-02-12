using Customer.Domain.Entities.TenantAggregate;
using Customer.Infrastructure.Persistence;
using Customer.Infrastructure.Persistence.Repositories.Write;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SharedKernel.Core.Pricing;
using Shouldly;

namespace Customer.UnitTests.Infrastructure.Persistence.Repositories;

public class TenantWriteRepositoryTests : IDisposable
{
    private readonly CustomerWriteDbContext _dbContext;
    private readonly TenantWriteRepository _repository;
    private readonly IHttpContextAccessor _httpContextAccessorMock;

    public TenantWriteRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<CustomerWriteDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _httpContextAccessorMock = Substitute.For<IHttpContextAccessor>();
        _dbContext = new TestCustomerWriteDbContext(options);
        _repository = new TenantWriteRepository(_dbContext, _httpContextAccessorMock);
    }

    // Test-specific DbContext that bypasses multi-tenant complications
    private class TestCustomerWriteDbContext : CustomerWriteDbContext
    {
        public TestCustomerWriteDbContext(DbContextOptions<CustomerWriteDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Skip base.OnModelCreating to avoid multi-tenant configuration issues in tests
            // Apply only the entity configurations we need
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(CustomerWriteDbContext).Assembly,
                type => type.FullName?.Contains("Config.Write", StringComparison.Ordinal) ?? false);
        }
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnTenant_WhenExists()
    {
        // Arrange
        var tenantResult = Tenant.Create(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL);

        var tenant = tenantResult.Value;
        await _dbContext.Tenants.AddAsync(tenant, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByIdAsync(tenant.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(tenant.Id);
        result.Identifier.ShouldBe("test-tenant");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(nonExistentId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdentifierAsync_ShouldReturnTenant_WhenExists()
    {
        // Arrange
        var tenantResult = Tenant.Create(
            "unique-tenant",
            "Unique Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL);

        var tenant = tenantResult.Value;
        await _dbContext.Tenants.AddAsync(tenant, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.GetByIdentifierAsync("unique-tenant", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Identifier.ShouldBe("unique-tenant");
        result.Name.ShouldBe("Unique Tenant");
    }

    [Fact]
    public async Task GetByIdentifierAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByIdentifierAsync("non-existent", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExistsByIdentifierAsync_ShouldReturnTrue_WhenExists()
    {
        // Arrange
        var tenantResult = Tenant.Create(
            "existing-tenant",
            "Existing Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL);

        var tenant = tenantResult.Value;
        await _dbContext.Tenants.AddAsync(tenant, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _repository.ExistsByIdentifierAsync("existing-tenant", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsByIdentifierAsync_ShouldReturnFalse_WhenNotExists()
    {
        // Act
        var result = await _repository.ExistsByIdentifierAsync("does-not-exist", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task AddAsync_ShouldAddTenant()
    {
        // Arrange
        var tenantResult = Tenant.Create(
            "new-tenant",
            "New Tenant",
            "Starter",
            DatabaseStrategy.Shared,
            DatabaseProvider.PostgreSQL);

        var tenant = tenantResult.Value;

        // Act
        await _repository.AddAsync(tenant, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var saved = await _dbContext.Tenants.FindAsync([tenant.Id], TestContext.Current.CancellationToken);
        saved.ShouldNotBeNull();
        saved.Identifier.ShouldBe("new-tenant");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateTenant()
    {
        // Arrange
        var tenantResult = Tenant.Create(
            "update-test",
            "Original Name",
            "Starter",
            DatabaseStrategy.Shared,
            DatabaseProvider.PostgreSQL);

        var tenant = tenantResult.Value;
        await _dbContext.Tenants.AddAsync(tenant, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Detach the entity to simulate a new context
        _dbContext.Entry(tenant).State = EntityState.Detached;

        // Get the tenant again and modify it
        var savedTenant = await _repository.GetByIdAsync(tenant.Id, TestContext.Current.CancellationToken);
        savedTenant.ShouldNotBeNull();

        // Act
        _repository.Update(savedTenant);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var updated = await _dbContext.Tenants.FindAsync([tenant.Id], TestContext.Current.CancellationToken);
        updated.ShouldNotBeNull();
        updated.Id.ShouldBe(tenant.Id);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveTenant()
    {
        // Arrange
        var tenantResult = Tenant.Create(
            "delete-test",
            "To Be Deleted",
            "Starter",
            DatabaseStrategy.Shared,
            DatabaseProvider.PostgreSQL);

        var tenant = tenantResult.Value;
        await _dbContext.Tenants.AddAsync(tenant, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        _repository.Delete(tenant);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var deleted = await _dbContext.Tenants.FindAsync([tenant.Id], TestContext.Current.CancellationToken);
        deleted.ShouldBeNull();
    }

    [Fact]
    public async Task Repository_ShouldHandleTenantWithDatabaseMetadata()
    {
        // Arrange
        var tenantResult = Tenant.Create(
            "with-metadata",
            "Tenant With Metadata",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL);

        var tenant = tenantResult.Value;
        tenant.AddDatabaseMetadata(
            "catalog",
            "database/tenants/guid/catalog/write",
            "database/tenants/guid/catalog/read",
            true);

        // Act
        await _repository.AddAsync(tenant, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var saved = await _dbContext.Tenants
            .Include(t => t.Databases)
            .FirstOrDefaultAsync(t => t.Id == tenant.Id, TestContext.Current.CancellationToken);

        saved.ShouldNotBeNull();
        saved.Databases.ShouldNotBeEmpty();
        saved.Databases.Count.ShouldBe(1);
        saved.Databases[0].ServiceName.ShouldBe("catalog");
    }

    [Fact]
    public async Task Repository_ShouldHandleTenantWithMigrationStatus()
    {
        // Arrange
        var tenantResult = Tenant.Create(
            "with-status",
            "Tenant With Status",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL);

        var tenant = tenantResult.Value;
        tenant.InitializeMigrationStatus("catalog");

        // Act
        await _repository.AddAsync(tenant, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var saved = await _dbContext.Tenants
            .Include(t => t.MigrationStatuses)
            .FirstOrDefaultAsync(t => t.Id == tenant.Id, TestContext.Current.CancellationToken);

        saved.ShouldNotBeNull();
        saved.MigrationStatuses.ShouldNotBeEmpty();
        saved.MigrationStatuses.Count.ShouldBe(1);
        saved.MigrationStatuses[0].ServiceName.ShouldBe("catalog");
        saved.MigrationStatuses[0].Status.ShouldBe(SharedKernel.Migration.Models.MigrationStatus.Pending);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
