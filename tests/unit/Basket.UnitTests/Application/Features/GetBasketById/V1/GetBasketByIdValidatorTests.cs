using Basket.Application.Basket.Features.GetBasketById.V1;
using Shouldly;

namespace Basket.UnitTests.Application.Features.GetBasketById.V1;

public sealed class GetBasketByIdValidatorTests
{
    [Fact]
    public void Validate_WhenRequestIsValid_ShouldSucceed()
    {
        GetBasketByIdValidator validator = new();
        GetBasketByIdRequest request = new()
        {
            BasketId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
        };

        var result = validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenBasketIdIsEmpty_ShouldFail()
    {
        GetBasketByIdValidator validator = new();
        GetBasketByIdRequest request = new()
        {
            BasketId = Guid.Empty,
            TenantId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
        };

        var result = validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.PropertyName == "BasketId");
    }
}
