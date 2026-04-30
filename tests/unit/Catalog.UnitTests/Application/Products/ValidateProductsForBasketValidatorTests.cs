using Catalog.Application.Products.Features.ValidateProductsForBasket.V1;
using Shouldly;

namespace Catalog.UnitTests.Application.Products;

public sealed class ValidateProductsForBasketValidatorTests
{
    private readonly ValidateProductsForBasketValidator validator = new();

    [Fact]
    public async Task Validate_ShouldPass_WhenItemsAreValid()
    {
        ValidateProductsForBasketRequest request = new();
        request.Items.Add(new ValidateProductsForBasketItemRequest
        {
            ProductId = Guid.NewGuid(),
            Quantity = 2,
        });

        var result = await this.validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenItemsAreEmpty()
    {
        ValidateProductsForBasketRequest request = new();

        var result = await this.validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_ShouldFail_WhenQuantityIsNotPositive(int quantity)
    {
        ValidateProductsForBasketRequest request = new();
        request.Items.Add(new ValidateProductsForBasketItemRequest
        {
            ProductId = Guid.NewGuid(),
            Quantity = quantity,
        });

        var result = await this.validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenProductIdIsEmpty()
    {
        ValidateProductsForBasketRequest request = new();
        request.Items.Add(new ValidateProductsForBasketItemRequest
        {
            ProductId = Guid.Empty,
            Quantity = 1,
        });

        var result = await this.validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }
}
