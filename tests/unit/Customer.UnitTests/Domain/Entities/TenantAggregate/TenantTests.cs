using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Events;
using ErrorOr;
using SharedKernel.Core.Pricing;

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
        var writeKey = "ConnectionStrings__Tenants__tenant-id__Write";
        var readKey = "ConnectionStrings__Tenants__tenant-id__Read";
        var hasSeparateReadDatabase = true;
 
        // Act
        tenant.AddDatabaseMetadata(serviceName, writeKey, readKey, hasSeparateReadDatabase);
 
        // Assert
        tenant.Databases.ShouldContain(db => db.ServiceName == serviceName);
        var metadata = tenant.Databases.First(db => db.ServiceName == serviceName);
        metadata.WriteEnvVarKey.ShouldBe(writeKey);
        metadata.ReadEnvVarKey.ShouldBe(readKey);
        metadata.HasSeparateReadDatabase.ShouldBe(hasSeparateReadDatabase);
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
