using Catalog.Domain.Entities.ProductAggregate.Events;
using Shouldly;

namespace Catalog.UnitTests.Domain.Products;

public class ProductCreatedDomainEventTests
{
    [Fact]
    public void Constructor_ShouldSetProperties_WhenValuesProvided()
    {
        // Arrange
        var productId = Guid.NewGuid();
        const string productName = "Laptop";

        // Act
        var domainEvent = new ProductCreatedDomainEvent(productId, productName);

        // Assert
        domainEvent.ProductId.ShouldBe(productId);
        domainEvent.Name.ShouldBe(productName);
    }
}
