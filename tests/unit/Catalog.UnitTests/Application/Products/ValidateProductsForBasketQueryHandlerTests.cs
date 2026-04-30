using Catalog.Application.Products.Features.ValidateProductsForBasket.V1;
using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Repositories;
using Catalog.Application.Promotions.ReadModels;
using Catalog.Application.Promotions.Repositories;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Application.Products;

public sealed class ValidateProductsForBasketQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnValidItem_WhenProductIsActiveAndHasPrice()
    {
        IProductReadRepository productRepository = Substitute.For<IProductReadRepository>();
        IProductPriceReadRepository productPriceRepository = Substitute.For<IProductPriceReadRepository>();
        IPromotionReadRepository promotionRepository = Substitute.For<IPromotionReadRepository>();

        Guid productId = Guid.NewGuid();
        productRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([
                new ProductReadModel
                {
                    Id = productId,
                    Name = "Active Product",
                    Sku = "SKU-001",
                    IsActive = true,
                },
            ]);

        productPriceRepository.GetByProductIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([
                new ProductPriceReadModel
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    SalePrice = 19.99m,
                    CurrencyCode = "USD",
                    ProductPriceTypeId = Guid.NewGuid(),
                },
            ]);

        promotionRepository.GetActivePromotionsAsync(Arg.Any<CancellationToken>())
            .Returns([
                new PromotionReadModel
                {
                    Id = Guid.NewGuid(),
                    Name = "Promo",
                    StartDate = DateTimeOffset.UtcNow.AddHours(-1),
                    EndDate = DateTimeOffset.UtcNow.AddHours(1),
                    IsActive = true,
                },
            ]);

        ValidateProductsForBasketQuery query = new(
            [
                new ValidateProductsForBasketItemRequest
                {
                    ProductId = productId,
                    Quantity = 2,
                },
            ]);

        ValidateProductsForBasketQueryHandler sut = new(productRepository, productPriceRepository, promotionRepository);

        var result = await sut.Handle(query, TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        result.Value.Items.Count.ShouldBe(1);
        result.Value.Items[0].ProductId.ShouldBe(productId);
        result.Value.Items[0].IsValid.ShouldBeTrue();
        result.Value.Items[0].UnitPrice.ShouldBe(19.99m);
        result.Value.Items[0].CurrencyCode.ShouldBe("USD");
        result.Value.Items[0].HasActiveRebate.ShouldBeTrue();
        result.Value.Items[0].FailureCode.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenProductDoesNotExist()
    {
        IProductReadRepository productRepository = Substitute.For<IProductReadRepository>();
        IProductPriceReadRepository productPriceRepository = Substitute.For<IProductPriceReadRepository>();
        IPromotionReadRepository promotionRepository = Substitute.For<IPromotionReadRepository>();

        productRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProductReadModel>());
        productPriceRepository.GetByProductIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProductPriceReadModel>());
        promotionRepository.GetActivePromotionsAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PromotionReadModel>());

        Guid missingProductId = Guid.NewGuid();
        ValidateProductsForBasketQuery query = new(
            [
                new ValidateProductsForBasketItemRequest
                {
                    ProductId = missingProductId,
                    Quantity = 1,
                },
            ]);

        ValidateProductsForBasketQueryHandler sut = new(productRepository, productPriceRepository, promotionRepository);

        var result = await sut.Handle(query, TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        result.Value.Items[0].Exists.ShouldBeFalse();
        result.Value.Items[0].IsValid.ShouldBeFalse();
        result.Value.Items[0].FailureCode.ShouldBe("product_not_found");
    }

    [Fact]
    public async Task Handle_ShouldReturnInvalid_WhenProductHasNoPrice()
    {
        IProductReadRepository productRepository = Substitute.For<IProductReadRepository>();
        IProductPriceReadRepository productPriceRepository = Substitute.For<IProductPriceReadRepository>();
        IPromotionReadRepository promotionRepository = Substitute.For<IPromotionReadRepository>();

        Guid productId = Guid.NewGuid();
        productRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([
                new ProductReadModel
                {
                    Id = productId,
                    Name = "No Price",
                    Sku = "SKU-002",
                    IsActive = true,
                },
            ]);
        productPriceRepository.GetByProductIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProductPriceReadModel>());
        promotionRepository.GetActivePromotionsAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PromotionReadModel>());

        ValidateProductsForBasketQuery query = new(
            [
                new ValidateProductsForBasketItemRequest
                {
                    ProductId = productId,
                    Quantity = 1,
                },
            ]);

        ValidateProductsForBasketQueryHandler sut = new(productRepository, productPriceRepository, promotionRepository);

        var result = await sut.Handle(query, TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        result.Value.Items[0].IsValid.ShouldBeFalse();
        result.Value.Items[0].FailureCode.ShouldBe("price_unavailable");
    }

    [Fact]
    public async Task Handle_ShouldReturnInvalid_WhenProductIsInactive()
    {
        IProductReadRepository productRepository = Substitute.For<IProductReadRepository>();
        IProductPriceReadRepository productPriceRepository = Substitute.For<IProductPriceReadRepository>();
        IPromotionReadRepository promotionRepository = Substitute.For<IPromotionReadRepository>();

        Guid productId = Guid.NewGuid();
        productRepository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([
                new ProductReadModel
                {
                    Id = productId,
                    Name = "Inactive",
                    Sku = "SKU-003",
                    IsActive = false,
                },
            ]);

        productPriceRepository.GetByProductIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([
                new ProductPriceReadModel
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    SalePrice = 12.00m,
                    CurrencyCode = "USD",
                    ProductPriceTypeId = Guid.NewGuid(),
                },
            ]);

        promotionRepository.GetActivePromotionsAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PromotionReadModel>());

        ValidateProductsForBasketQuery query = new(
            [
                new ValidateProductsForBasketItemRequest
                {
                    ProductId = productId,
                    Quantity = 1,
                },
            ]);

        ValidateProductsForBasketQueryHandler sut = new(productRepository, productPriceRepository, promotionRepository);

        var result = await sut.Handle(query, TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        result.Value.Items[0].IsActive.ShouldBeFalse();
        result.Value.Items[0].IsValid.ShouldBeFalse();
        result.Value.Items[0].FailureCode.ShouldBe("product_inactive");
    }
}
