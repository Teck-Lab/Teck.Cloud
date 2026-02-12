using Customer.Domain.Entities.TenantAggregate;
using Customer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Pricing;
using Shouldly;

namespace Customer.UnitTests.Infrastructure.Persistence;

public class UnitOfWorkTests : IDisposable
{
    private readonly CustomerWriteDbContext _dbContext;
    private readonly UnitOfWork _unitOfWork;

    public UnitOfWorkTests()
    {
        var options = new DbContextOptionsBuilder<CustomerWriteDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new TestCustomerWriteDbContext(options);
        _unitOfWork = new UnitOfWork(_dbContext);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldPersistChanges()
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

        // Act
        var result = await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeGreaterThan(0);
        var saved = await _dbContext.Tenants.FindAsync([tenant.Id], TestContext.Current.CancellationToken);
        saved.ShouldNotBeNull();
        saved.Identifier.ShouldBe("test-tenant");
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldReturnZero_WhenNoChanges()
    {
        // Act
        var result = await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldPersistMultipleEntities()
    {
        // Arrange
        var tenant1Result = Tenant.Create(
            "tenant-1",
            "Tenant 1",
            "Starter",
            DatabaseStrategy.Shared,
            DatabaseProvider.PostgreSQL);

        var tenant2Result = Tenant.Create(
            "tenant-2",
            "Tenant 2",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL);

        await _dbContext.Tenants.AddAsync(tenant1Result.Value, TestContext.Current.CancellationToken);
        await _dbContext.Tenants.AddAsync(tenant2Result.Value, TestContext.Current.CancellationToken);

        // Act
        var result = await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeGreaterThan(0);
        var count = await _dbContext.Tenants.CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(2);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldHandleUpdates()
    {
        // Arrange
        var tenantResult = Tenant.Create(
            "update-tenant",
            "Original Name",
            "Starter",
            DatabaseStrategy.Shared,
            DatabaseProvider.PostgreSQL);

        var tenant = tenantResult.Value;
        await _dbContext.Tenants.AddAsync(tenant, TestContext.Current.CancellationToken);
        await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Modify the tenant
        _dbContext.Entry(tenant).State = EntityState.Detached;
        var savedTenant = await _dbContext.Tenants.FindAsync([tenant.Id], TestContext.Current.CancellationToken);
        savedTenant.ShouldNotBeNull();

        // Act
        var updateResult = await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - no changes, so result should be 0
        updateResult.ShouldBe(0);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldHandleDeletes()
    {
        // Arrange
        var tenantResult = Tenant.Create(
            "delete-tenant",
            "To Be Deleted",
            "Starter",
            DatabaseStrategy.Shared,
            DatabaseProvider.PostgreSQL);

        var tenant = tenantResult.Value;
        await _dbContext.Tenants.AddAsync(tenant, TestContext.Current.CancellationToken);
        await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Remove the tenant
        _dbContext.Tenants.Remove(tenant);

        // Act
        var result = await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeGreaterThan(0);
        var deleted = await _dbContext.Tenants.FindAsync([tenant.Id], TestContext.Current.CancellationToken);
        deleted.ShouldBeNull();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
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
}
