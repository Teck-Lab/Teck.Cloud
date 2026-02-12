using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Events;
using ErrorOr;
using SharedKernel.Core.Pricing;
using SharedKernel.Migration.Models;
using Shouldly;

namespace Customer.UnitTests.Domain.Entities.TenantAggregate;

public class TenantTests
{
    [Fact]
    public void Create_ShouldReturnTenant_WhenValidInputProvided()
    {
        // Arrange
        var identifier = "test-tenant";
        var name = "Test Tenant";
        var plan = "Enterprise";
        var strategy = DatabaseStrategy.Dedicated;
        var provider = DatabaseProvider.PostgreSQL;

        // Act
        ErrorOr<Tenant> result = Tenant.Create(identifier, name, plan, strategy, provider);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldNotBeNull();
        result.Value.Identifier.ShouldBe(identifier);
        result.Value.Name.ShouldBe(name);
        result.Value.Plan.ShouldBe(plan);
        result.Value.DatabaseStrategy.ShouldBe(strategy);
        result.Value.DatabaseProvider.ShouldBe(provider);
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_ShouldRaiseTenantCreatedDomainEvent()
    {
        // Arrange
        var identifier = "test-tenant";
        var name = "Test Tenant";
        var plan = "Enterprise";
        var strategy = DatabaseStrategy.Dedicated;
        var provider = DatabaseProvider.PostgreSQL;

        // Act
        ErrorOr<Tenant> result = Tenant.Create(identifier, name, plan, strategy, provider);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.DomainEvents.ShouldNotBeEmpty();
        result.Value.DomainEvents.ShouldContain(e => e is TenantCreatedDomainEvent);
        
        var domainEvent = result.Value.DomainEvents.OfType<TenantCreatedDomainEvent>().First();
        domainEvent.TenantId.ShouldBe(result.Value.Id);
        domainEvent.Identifier.ShouldBe(identifier);
        domainEvent.Name.ShouldBe(name);
        domainEvent.DatabaseStrategy.ShouldBe(strategy.Name);
        domainEvent.DatabaseProvider.ShouldBe(provider.Name);
    }

    [Fact]
    public void Create_ShouldReturnError_WhenIdentifierIsEmpty()
    {
        // Arrange
        var strategy = DatabaseStrategy.Dedicated;
        var provider = DatabaseProvider.PostgreSQL;

        // Act
        ErrorOr<Tenant> result = Tenant.Create("", "Test Tenant", "Enterprise", strategy, provider);

        // Assert
        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public void Create_ShouldReturnError_WhenNameIsEmpty()
    {
        // Arrange
        var strategy = DatabaseStrategy.Dedicated;
        var provider = DatabaseProvider.PostgreSQL;

        // Act
        ErrorOr<Tenant> result = Tenant.Create("test-tenant", "", "Enterprise", strategy, provider);

        // Assert
        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public void Create_ShouldReturnError_WhenPlanIsEmpty()
    {
        // Arrange
        var strategy = DatabaseStrategy.Dedicated;
        var provider = DatabaseProvider.PostgreSQL;

        // Act
        ErrorOr<Tenant> result = Tenant.Create("test-tenant", "Test Tenant", "", strategy, provider);

        // Assert
        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public void AddDatabaseMetadata_ShouldAddMetadata_WhenValidInputProvided()
    {
        // Arrange
        var tenant = Tenant.Create(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL).Value;
        
        var serviceName = "catalog";
        var vaultWritePath = "database/tenants/tenant-id/catalog/write";
        var vaultReadPath = "database/tenants/tenant-id/catalog/read";
        var hasSeparateReadDatabase = true;

        // Act
        tenant.AddDatabaseMetadata(serviceName, vaultWritePath, vaultReadPath, hasSeparateReadDatabase);

        // Assert
        tenant.Databases.ShouldContain(db => db.ServiceName == serviceName);
        var metadata = tenant.Databases.First(db => db.ServiceName == serviceName);
        metadata.VaultWritePath.ShouldBe(vaultWritePath);
        metadata.VaultReadPath.ShouldBe(vaultReadPath);
        metadata.HasSeparateReadDatabase.ShouldBe(hasSeparateReadDatabase);
    }

    [Fact]
    public void InitializeMigrationStatus_ShouldAddMigrationStatus_WhenServiceNameProvided()
    {
        // Arrange
        var tenant = Tenant.Create(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL).Value;
        
        var serviceName = "catalog";

        // Act
        tenant.InitializeMigrationStatus(serviceName);

        // Assert
        tenant.MigrationStatuses.ShouldContain(ms => ms.ServiceName == serviceName);
        var status = tenant.MigrationStatuses.First(ms => ms.ServiceName == serviceName);
        status.Status.ShouldBe(MigrationStatus.Pending);
        status.LastMigrationVersion.ShouldBeNull();
        status.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void UpdateMigrationStatus_ShouldUpdateStatus_WhenStatusExists()
    {
        // Arrange
        var tenant = Tenant.Create(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL).Value;
        
        var serviceName = "catalog";
        tenant.InitializeMigrationStatus(serviceName);
        
        var newStatus = MigrationStatus.Completed;
        var lastMigrationVersion = "0001_InitialMigration";

        // Act
        var result = tenant.UpdateMigrationStatus(serviceName, newStatus, lastMigrationVersion, null);

        // Assert
        result.IsError.ShouldBeFalse();
        var status = tenant.MigrationStatuses.First(ms => ms.ServiceName == serviceName);
        status.Status.ShouldBe(newStatus);
        status.LastMigrationVersion.ShouldBe(lastMigrationVersion);
    }

    [Fact]
    public void UpdateMigrationStatus_ShouldReturnError_WhenServiceNotFound()
    {
        // Arrange
        var tenant = Tenant.Create(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL).Value;
        
        var serviceName = "nonexistent";

        // Act
        var result = tenant.UpdateMigrationStatus(serviceName, MigrationStatus.Completed, null, null);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Tenant.MigrationStatusNotFound");
    }

    [Fact]
    public void Activate_ShouldSetIsActiveToTrue()
    {
        // Arrange
        var tenant = Tenant.Create(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL).Value;
        
        tenant.Deactivate();

        // Act
        tenant.Activate();

        // Assert
        tenant.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveToFalse()
    {
        // Arrange
        var tenant = Tenant.Create(
            "test-tenant",
            "Test Tenant",
            "Enterprise",
            DatabaseStrategy.Dedicated,
            DatabaseProvider.PostgreSQL).Value;

        // Act
        tenant.Deactivate();

        // Assert
        tenant.IsActive.ShouldBeFalse();
    }
}
