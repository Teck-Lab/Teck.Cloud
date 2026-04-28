using Catalog.Application.Products.Features.GetPaginatedProducts.V1;
using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Repositories;
using NSubstitute;
using SharedKernel.Core.Pagination;
using Shouldly;

namespace Catalog.UnitTests.Application.Products;

public sealed class GetPaginatedProductsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnMappedPagedProducts_WhenProductsExist()
    {
        // Arrange
        IProductReadRepository repository = Substitute.For<IProductReadRepository>();
        GetPaginatedProductsQuery query = new(1, 10, "lap");

        PagedList<ProductReadModel> pagedProducts = new(
            [
                new ProductReadModel
                {
                    Id = Guid.NewGuid(),
                    Name = "Laptop",
                    Description = "Powerful laptop",
                    Sku = "SKU-001",
                    BrandName = "Brand A",
                },
            ],
            totalItems: 1,
            page: 1,
            size: 10);

        repository.GetPagedProductsAsync(query.Page, query.Size, query.Keyword, Arg.Any<CancellationToken>())
            .Returns(pagedProducts);

        GetPaginatedProductsQueryHandler sut = new(repository);

        // Act
        var result = await sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.TotalItems.ShouldBe(1);
        result.Value.Items.Count.ShouldBe(1);
        result.Value.Items[0].Name.ShouldBe("Laptop");
        result.Value.Items[0].Sku.ShouldBe("SKU-001");

        await repository.Received(1)
            .GetPagedProductsAsync(query.Page, query.Size, query.Keyword, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyPagedProducts_WhenProductsDoNotExist()
    {
        // Arrange
        IProductReadRepository repository = Substitute.For<IProductReadRepository>();
        GetPaginatedProductsQuery query = new(1, 10, null);

        repository.GetPagedProductsAsync(query.Page, query.Size, query.Keyword, Arg.Any<CancellationToken>())
            .Returns(new PagedList<ProductReadModel>([], 0, 1, 10));

        GetPaginatedProductsQueryHandler sut = new(repository);

        // Act
        var result = await sut.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.TotalItems.ShouldBe(0);
        result.Value.Items.ShouldBeEmpty();
    }
}
