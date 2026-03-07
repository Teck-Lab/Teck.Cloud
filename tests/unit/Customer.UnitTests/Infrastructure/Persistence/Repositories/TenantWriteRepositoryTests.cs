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
            CreateArgs(
                "test-tenant",
                "Test Tenant",
                "Enterprise",
                DatabaseStrategy.Dedicated,
                DatabaseProvider.PostgreSQL));

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
            CreateArgs(
                "unique-tenant",
                "Unique Tenant",
                "Enterprise",
                DatabaseStrategy.Dedicated,
                DatabaseProvider.PostgreSQL));

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
            CreateArgs(
                "existing-tenant",
                "Existing Tenant",
                "Enterprise",
                DatabaseStrategy.Dedicated,
                DatabaseProvider.PostgreSQL));

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
            CreateArgs(
                "new-tenant",
                "New Tenant",
                "Starter",
                DatabaseStrategy.Shared,
                DatabaseProvider.PostgreSQL));

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
            CreateArgs(
                "update-test",
                "Original Name",
                "Starter",
                DatabaseStrategy.Shared,
                DatabaseProvider.PostgreSQL));

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
            CreateArgs(
                "delete-test",
                "To Be Deleted",
                "Starter",
                DatabaseStrategy.Shared,
                DatabaseProvider.PostgreSQL));

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
            CreateArgs(
                "with-metadata",
                "Tenant With Metadata",
                "Enterprise",
                DatabaseStrategy.Dedicated,
                DatabaseProvider.PostgreSQL));

        var tenant = tenantResult.Value;
        tenant.AddDatabaseMetadata(CreateMetadataArgs(
            "catalog",
            "database/tenants/guid/catalog/write",
            "database/tenants/guid/catalog/read",
            true));

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
    public async Task Repository_ShouldRoundTrip_ReadDatabaseMode_AndComputedSeparateReadFlag()
    {
        // Arrange
        var tenantResult = Tenant.Create(
            CreateArgs(
                "mode-roundtrip",
                "Mode Roundtrip",
                "Enterprise",
                DatabaseStrategy.Dedicated,
                DatabaseProvider.PostgreSQL));

        var tenant = tenantResult.Value;
        tenant.AddDatabaseMetadata(CreateMetadataArgs(
            "catalog",
            "database/tenants/guid/catalog/write",
            "database/tenants/guid/catalog/read",
            true));
        tenant.AddDatabaseMetadata(CreateMetadataArgs(
            "customer",
            "database/tenants/guid/customer/write",
            null,
            false));

        // Act
        await _repository.AddAsync(tenant, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Assert
        var saved = await _dbContext.Tenants
            .Include(t => t.Databases)
            .FirstOrDefaultAsync(t => t.Id == tenant.Id, TestContext.Current.CancellationToken);

        saved.ShouldNotBeNull();
        saved.Databases.Count.ShouldBe(2);

        var catalogDatabase = saved.Databases.Single(d => d.ServiceName == "catalog");
        catalogDatabase.ReadDatabaseMode.ShouldBe(ReadDatabaseMode.SeparateRead);
        catalogDatabase.HasSeparateReadDatabase.ShouldBeTrue();

        var customerDatabase = saved.Databases.Single(d => d.ServiceName == "customer");
        customerDatabase.ReadDatabaseMode.ShouldBe(ReadDatabaseMode.SharedWrite);
        customerDatabase.HasSeparateReadDatabase.ShouldBeFalse();
    }


    public void Dispose()
    {
        _dbContext?.Dispose();
    }

    private static TenantCreateArgs CreateArgs(
        string identifier,
        string name,
        string plan,
        DatabaseStrategy strategy,
        DatabaseProvider provider)
    {
        return new TenantCreateArgs
        {
            Identifier = identifier,
            Name = name,
            Plan = plan,
            Database = new TenantCreateDatabaseSettings
            {
                DatabaseStrategy = strategy,
                DatabaseProvider = provider,
            },
        };
    }

    private static TenantDatabaseMetadataArgs CreateMetadataArgs(
        string serviceName,
        string writeKey,
        string? readKey,
        bool hasSeparateReadDatabase)
    {
        return new TenantDatabaseMetadataArgs
        {
            ServiceName = serviceName,
            WriteEnvVarKey = writeKey,
            ReadEnvVarKey = readKey,
            ReadDatabaseMode = hasSeparateReadDatabase ? ReadDatabaseMode.SeparateRead : ReadDatabaseMode.SharedWrite,
        };
    }
}
