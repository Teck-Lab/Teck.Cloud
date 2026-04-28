using Catalog.Application.Products.Features.GetProductsByCategory.V1;
using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Repositories;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Application.Products;

public sealed class GetProductsByCategoryQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnMappedProducts_WhenProductsExistForCategory()
    {
        // Arrange
        IProductReadRepository repository = Substitute.For<IProductReadRepository>();
        Guid categoryId = Guid.NewGuid();
        GetProductsByCategoryQuery query = new(categoryId);

        repository.GetByCategoryIdAsync(categoryId, Arg.Any<CancellationToken>())
            .Returns([
                new ProductReadModel
                {
                    Id = Guid.NewGuid(),
                    Name = "Product C",
                    Description = "Description C",
                    Sku = "SKU-C",
                    CategoryId = categoryId,
                    CategoryName = "Category C",
                },
            ]);

        GetProductsByCategoryQueryHandler sut = new(repository);

        // Act
        var result = await sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Count.ShouldBe(1);
        result.Value[0].Name.ShouldBe("Product C");
        result.Value[0].CategoryId.ShouldBe(categoryId);
        result.Value[0].Sku.ShouldBe("SKU-C");
    }
}
