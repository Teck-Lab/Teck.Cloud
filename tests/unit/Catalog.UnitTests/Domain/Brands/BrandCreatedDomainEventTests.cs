using Catalog.Domain.Entities.BrandAggregate.Events;
using Shouldly;

namespace Catalog.UnitTests.Domain.Brands;

public class BrandCreatedDomainEventTests
{
    [Fact]
    public void Constructor_ShouldSetProperties_WhenValuesProvided()
    {
        // Arrange
        var brandId = Guid.NewGuid();
        const string brandName = "Contoso";

        // Act
        var domainEvent = new BrandCreatedDomainEvent(brandId, brandName);

        // Assert
        domainEvent.BrandId.ShouldBe(brandId);
        domainEvent.BrandName.ShouldBe(brandName);
    }
}
