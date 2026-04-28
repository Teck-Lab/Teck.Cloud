using Catalog.Application.Products.Features.GetProductsByBrand.V1;
using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Repositories;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Application.Products;

public sealed class GetProductsByBrandQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnMappedProducts_WhenProductsExistForBrand()
    {
        // Arrange
        IProductReadRepository repository = Substitute.For<IProductReadRepository>();
        Guid brandId = Guid.NewGuid();
        GetProductsByBrandQuery query = new(brandId);

        repository.GetByBrandIdAsync(brandId, Arg.Any<CancellationToken>())
            .Returns([
                new ProductReadModel
                {
                    Id = Guid.NewGuid(),
                    Name = "Product A",
                    Description = "Description A",
                    Sku = "SKU-A",
                    BrandId = brandId,
                    BrandName = "Brand A",
                },
            ]);

        GetProductsByBrandQueryHandler sut = new(repository);

        // Act
        var result = await sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Count.ShouldBe(1);
        result.Value[0].Name.ShouldBe("Product A");
        result.Value[0].BrandId.ShouldBe(brandId);
        result.Value[0].Sku.ShouldBe("SKU-A");
    }
}
