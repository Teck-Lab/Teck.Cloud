using Customer.Domain.Entities.TenantAggregate.Events;
using Shouldly;

namespace Customer.UnitTests.Domain.Entities.TenantAggregate.Events;

public class TenantCreatedDomainEventTests
{
    [Fact]
    public void Constructor_ShouldSetProperties_WhenDetailsProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        TenantCreatedEventDetails details = new()
        {
            TenantId = tenantId,
            Identifier = "tenant-01",
            Name = "Tenant 01",
            DatabaseStrategy = "Dedicated",
            DatabaseProvider = "PostgreSQL",
        };

        // Act
        var domainEvent = new TenantCreatedDomainEvent(details);

        // Assert
        domainEvent.TenantId.ShouldBe(tenantId);
        domainEvent.Identifier.ShouldBe("tenant-01");
        domainEvent.Name.ShouldBe("Tenant 01");
        domainEvent.DatabaseStrategy.ShouldBe("Dedicated");
        domainEvent.DatabaseProvider.ShouldBe("PostgreSQL");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenDetailsIsNull()
    {
        // Act
        var exception = Should.Throw<ArgumentNullException>(() => new TenantCreatedDomainEvent(null!));

        // Assert
        exception.ParamName.ShouldBe("details");
    }
}
