using Location.Application.Service.Features.GetResolvedTemplate.V1;
using Shouldly;

namespace Location.UnitTests.Application.Service.Features.GetResolvedTemplate.V1;

public sealed class GetResolvedTemplateValidatorTests
{
    private readonly GetResolvedTemplateValidator validator = new();

    [Fact]
    public void Validate_WhenLocationNodeIdIsProvided_ShouldSucceed()
    {
        GetResolvedTemplateQuery request = new("loc-1", null);

        var result = this.validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenLocationNodeIdIsMissing_ShouldFail()
    {
        GetResolvedTemplateQuery request = new(string.Empty, null);

        var result = this.validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.PropertyName == "LocationNodeId");
    }
}
