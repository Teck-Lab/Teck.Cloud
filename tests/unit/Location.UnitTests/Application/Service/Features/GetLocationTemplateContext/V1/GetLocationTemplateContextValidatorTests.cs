using Location.Application.Service.Features.GetLocationTemplateContext.V1;
using Shouldly;

namespace Location.UnitTests.Application.Service.Features.GetLocationTemplateContext.V1;

public sealed class GetLocationTemplateContextValidatorTests
{
    private readonly GetLocationTemplateContextValidator validator = new();

    [Fact]
    public void Validate_WhenLocationNodeIdIsProvided_ShouldSucceed()
    {
        GetLocationTemplateContextRequest request = new()
        {
            LocationNodeId = "loc-1",
        };

        var result = this.validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenLocationNodeIdIsMissing_ShouldFail()
    {
        GetLocationTemplateContextRequest request = new();

        var result = this.validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.PropertyName == "LocationNodeId");
    }
}
