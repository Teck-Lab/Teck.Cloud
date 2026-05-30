using Order.Application.Orders.Features.CreateOrderFromBasket.V1;
using Shouldly;

namespace Order.UnitTests.Application.Orders.Features.CreateOrderFromBasket.V1;

public sealed class CreateOrderFromBasketValidatorTests
{
    private readonly CreateOrderFromBasketValidator validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_ShouldSucceed()
    {
        CreateOrderFromBasketRequest request = new()
        {
            TenantId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            BasketId = Guid.NewGuid(),
        };

        var result = this.validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenRequestHasEmptyGuids_ShouldFail()
    {
        CreateOrderFromBasketRequest request = new();

        var result = this.validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(3);
    }
}
