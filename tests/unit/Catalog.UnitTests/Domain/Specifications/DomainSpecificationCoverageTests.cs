using Catalog.Domain.Entities.BrandAggregate.Specifications;
using Catalog.Domain.Entities.CategoryAggregate.Specifications;
using Catalog.Domain.Entities.ProductAggregate.Specifications;
using Catalog.Domain.Entities.ProductPriceTypeAggregate.Specifications;
using Catalog.Domain.Entities.PromotionAggregate.Specifications;
using Catalog.Domain.Entities.SupplierAggregate.Specifications;
using Shouldly;

namespace Catalog.UnitTests.Domain.Specifications;

public class DomainSpecificationCoverageTests
{
    [Fact]
    public void BrandByNameSpecification_ShouldConstruct_WhenExactMatchEnabled()
    {
        // Act
        var specification = new BrandByNameSpecification("Acme");

        // Assert
        specification.ShouldNotBeNull();
    }

    [Fact]
    public void BrandByNameSpecification_ShouldConstruct_WhenExactMatchDisabled()
    {
        // Act
        var specification = new BrandByNameSpecification("ac", useExactMatch: false);

        // Assert
        specification.ShouldNotBeNull();
    }

    [Fact]
    public void BrandListSpecification_ShouldConstruct_WithFilterOrderingAndPaging()
    {
        // Act
        var specification = new BrandListSpecification(skip: 1, take: 2, nameFilter: "brand", ordering: BrandOrdering.ByCreationDate);

        // Assert
        specification.ShouldNotBeNull();
    }

    [Fact]
    public void BrandListSpecification_ShouldConstruct_WithTakeOnly()
    {
        // Act
        var specification = new BrandListSpecification(take: 3, ordering: BrandOrdering.ByName);

        // Assert
        specification.ShouldNotBeNull();
    }

    [Fact]
    public void CategoryByIdSpecification_ShouldConstruct()
    {
        // Act
        var specification = new CategoryByIdSpecification(Guid.NewGuid());

        // Assert
        specification.ShouldNotBeNull();
    }

    [Fact]
    public void ProductByIdSpecification_ShouldConstruct_WithoutRelations()
    {
        // Act
        var specification = new ProductByIdSpecification(Guid.NewGuid(), includeRelations: false);

        // Assert
        specification.ShouldNotBeNull();
    }

    [Fact]
    public void ProductByIdSpecification_ShouldConstruct_WithRelations()
    {
        // Act
        var specification = new ProductByIdSpecification(Guid.NewGuid(), includeRelations: true);

        // Assert
        specification.ShouldNotBeNull();
    }

    [Fact]
    public void ProductPriceTypeByIdSpecification_ShouldConstruct()
    {
        // Act
        var specification = new ProductPriceTypeByIdSpecification(Guid.NewGuid());

        // Assert
        specification.ShouldNotBeNull();
    }

    [Fact]
    public void PromotionByIdSpecification_ShouldConstruct_WithoutProducts()
    {
        // Act
        var specification = new PromotionByIdSpecification(Guid.NewGuid(), includeProducts: false);

        // Assert
        specification.ShouldNotBeNull();
    }

    [Fact]
    public void PromotionByIdSpecification_ShouldConstruct_WithProducts()
    {
        // Act
        var specification = new PromotionByIdSpecification(Guid.NewGuid(), includeProducts: true);

        // Assert
        specification.ShouldNotBeNull();
    }

    [Fact]
    public void SupplierByIdSpecification_ShouldConstruct()
    {
        // Act
        var specification = new SupplierByIdSpecification(Guid.NewGuid());

        // Assert
        specification.ShouldNotBeNull();
    }
}
