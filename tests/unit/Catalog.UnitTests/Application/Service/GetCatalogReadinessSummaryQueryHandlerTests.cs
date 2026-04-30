using Catalog.Application.Brands.ReadModels;
using Catalog.Application.Brands.Repositories;
using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Repositories;
using Catalog.Application.Promotions.ReadModels;
using Catalog.Application.Promotions.Repositories;
using Catalog.Application.Service.Features.GetCatalogReadinessSummary.V1;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Application.Service;

public sealed class GetCatalogReadinessSummaryQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnExpectedCounters()
    {
        // Arrange
        IBrandReadRepository brandReadRepository = Substitute.For<IBrandReadRepository>();
        IProductReadRepository productReadRepository = Substitute.For<IProductReadRepository>();
        IPromotionReadRepository promotionReadRepository = Substitute.For<IPromotionReadRepository>();

        brandReadRepository
            .GetAllAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns([
                new BrandReadModel { Id = Guid.NewGuid(), Name = "Brand 1" },
                new BrandReadModel { Id = Guid.NewGuid(), Name = "Brand 2" },
            ]);

        productReadRepository
            .GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([
                new ProductReadModel { Id = Guid.NewGuid(), Name = "Product 1", Sku = "SKU-1" },
                new ProductReadModel { Id = Guid.NewGuid(), Name = "Product 2", Sku = "SKU-2" },
                new ProductReadModel { Id = Guid.NewGuid(), Name = "Product 3", Sku = "SKU-3" },
            ]);

        promotionReadRepository
            .GetActivePromotionsAsync(Arg.Any<CancellationToken>())
            .Returns([
                new PromotionReadModel { Id = Guid.NewGuid(), Name = "Promo 1" },
            ]);

        GetCatalogReadinessSummaryQueryHandler sut = new(
            brandReadRepository,
            productReadRepository,
            promotionReadRepository);

        // Act
        var result = await sut.Handle(new GetCatalogReadinessSummaryQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.BrandCount.ShouldBe(2);
        result.Value.ProductCount.ShouldBe(3);
        result.Value.ActivePromotionCount.ShouldBe(1);
    }
}
