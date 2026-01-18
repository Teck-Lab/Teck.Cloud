using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Domain.Entities.ProductPriceTypeAggregate;
using Shouldly;

namespace Catalog.UnitTests.Domain.ProductPrices;

public class ProductPriceTests
{
    private readonly IFixture _fixture;

    public ProductPriceTests()
    {
        _fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
    }

    [Fact]
    public void Create_Should_SetCorrectProperties()
    {
        // Arrange
        var salePrice = _fixture.Create<decimal>();
        var currencyCode = _fixture.Create<string>();
        var product = _fixture.Create<Product>();
        var priceType = _fixture.Create<ProductPriceType>();

        // Act
        var result = ProductPrice.Create(product.Id, salePrice, currencyCode, priceType.Id);

        // Assert
        result.IsError.ShouldBeFalse();
        var price = result.Value;
        price.SalePrice.ShouldBe(salePrice);
        price.CurrencyCode.ShouldBe(currencyCode);
        price.ProductId.ShouldBe(product.Id);
        price.ProductPriceTypeId.ShouldBe(priceType.Id);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_Should_ReturnError_When_PriceIsNegative(decimal invalidPrice)
    {
        // Arrange
        var currencyCode = _fixture.Create<string>();
        var product = _fixture.Create<Product>();
        var priceType = _fixture.Create<ProductPriceType>();

        // Act
        var result = ProductPrice.Create(product.Id, invalidPrice, currencyCode, priceType.Id);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("cannot be negative");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.0)]
    public void Create_Should_Allow_ZeroPrice(decimal zeroPrice)
    {
        var currencyCode = _fixture.Create<string>();
        var product = _fixture.Create<Product>();
        var priceType = _fixture.Create<ProductPriceType>();
        var result = ProductPrice.Create(product.Id, zeroPrice, currencyCode, priceType.Id);
        result.IsError.ShouldBeFalse();
        result.Value.SalePrice.ShouldBe(zeroPrice);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_ReturnError_When_CurrencyCodeIsInvalid(string invalidCurrency)
    {
        var salePrice = _fixture.Create<decimal>();
        var product = _fixture.Create<Product>();
        var priceType = _fixture.Create<ProductPriceType>();
        var result = ProductPrice.Create(product.Id, salePrice, invalidCurrency, priceType.Id);
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("currency code");
    }

    [Fact]
    public void Create_Should_ReturnError_When_ProductIdIsDefault()
    {
        var salePrice = _fixture.Create<decimal>();
        var currencyCode = _fixture.Create<string>();
        var priceType = _fixture.Create<ProductPriceType>();
        var result = ProductPrice.Create(Guid.Empty, salePrice, currencyCode, priceType.Id);
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("ProductId");
    }

    [Fact]
    public void Create_Should_ReturnError_When_ProductPriceTypeIdIsDefault()
    {
        var salePrice = _fixture.Create<decimal>();
        var currencyCode = _fixture.Create<string>();
        var product = _fixture.Create<Product>();
        var result = ProductPrice.Create(product.Id, salePrice, currencyCode, Guid.Empty);
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("ProductPriceTypeId");
    }

    [Fact]
    public void Create_Should_ReturnMultipleErrors_When_MultipleFieldsInvalid()
    {
        var result = ProductPrice.Create(Guid.Empty, -1, null, Guid.Empty);
        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Update_Should_Succeed_When_AllFieldsNull()
    {
        var salePrice = 10m;
        var currencyCode = "USD";
        var product = _fixture.Create<Product>();
        var priceType = _fixture.Create<ProductPriceType>();
        var createResult = ProductPrice.Create(product.Id, salePrice, currencyCode, priceType.Id);
        var price = createResult.Value;
        var originalSalePrice = price.SalePrice;
        var originalCurrency = price.CurrencyCode;
        var updateResult = price.Update(null, null);
        updateResult.IsError.ShouldBeFalse();
        price.SalePrice.ShouldBe(originalSalePrice);
        price.CurrencyCode.ShouldBe(originalCurrency);
    }

    [Fact]
    public void Update_Should_ReturnError_When_SalePriceIsNegative()
    {
        var salePrice = 10m;
        var currencyCode = "USD";
        var product = _fixture.Create<Product>();
        var priceType = _fixture.Create<ProductPriceType>();
        var createResult = ProductPrice.Create(product.Id, salePrice, currencyCode, priceType.Id);
        var price = createResult.Value;
        var updateResult = price.Update(-1, null);
        updateResult.IsError.ShouldBeTrue();
        updateResult.FirstError.Description.ShouldContain("negative");
    }

    [Fact]
    public void Update_Should_NotChange_When_ValuesAreSame()
    {
        var salePrice = 10m;
        var currencyCode = "USD";
        var product = _fixture.Create<Product>();
        var priceType = _fixture.Create<ProductPriceType>();
        var createResult = ProductPrice.Create(product.Id, salePrice, currencyCode, priceType.Id);
        var price = createResult.Value;
        var updateResult = price.Update(salePrice, currencyCode);
        updateResult.IsError.ShouldBeFalse();
        price.SalePrice.ShouldBe(salePrice);
        price.CurrencyCode.ShouldBe(currencyCode);
    }

    [Fact]
    public void Update_Should_Change_SalePrice_And_CurrencyCode()
    {
        var salePrice = 10m;
        var currencyCode = "USD";
        var product = _fixture.Create<Product>();
        var priceType = _fixture.Create<ProductPriceType>();
        var createResult = ProductPrice.Create(product.Id, salePrice, currencyCode, priceType.Id);
        var price = createResult.Value;
        var newSalePrice = 20m;
        var newCurrency = "EUR";
        var updateResult = price.Update(newSalePrice, newCurrency);
        updateResult.IsError.ShouldBeFalse();
        price.SalePrice.ShouldBe(newSalePrice);
        price.CurrencyCode.ShouldBe(newCurrency);
    }
}