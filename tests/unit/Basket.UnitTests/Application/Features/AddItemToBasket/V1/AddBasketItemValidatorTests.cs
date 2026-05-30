using Basket.Application.Basket.Features.AddItemToBasket.V1;
using Shouldly;

namespace Basket.UnitTests.Application.Features.AddItemToBasket.V1;

public sealed class AddBasketItemValidatorTests
{
    [Fact]
    public void Validate_WhenRequestIsValid_ShouldSucceed()
    {
        AddBasketItemValidator validator = new();
        AddBasketItemRequest request = new()
        {
            TenantId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Quantity = 1,
        };

        var result = validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenFieldsMissing_ShouldReturnValidationErrors()
    {
        AddBasketItemValidator validator = new();
        AddBasketItemRequest request = new();

        var result = validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void Validate_WhenQuantityBoundaryValues_ShouldValidateCorrectly()
    {
        AddBasketItemValidator validator = new();
        AddBasketItemRequest zeroRequest = new()
        {
            TenantId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Quantity = 0,
        };

        AddBasketItemRequest oneRequest = new() { TenantId = zeroRequest.TenantId, CustomerId = zeroRequest.CustomerId, ProductId = zeroRequest.ProductId, Quantity = 1 };

        validator.Validate(zeroRequest).IsValid.ShouldBeFalse();
        validator.Validate(oneRequest).IsValid.ShouldBeTrue();
    }
}
