using Location.Application.Service.Features.UpsertTemplateDesign.V1;
using Shouldly;

namespace Location.UnitTests.Application.Service.Features.UpsertTemplateDesign.V1;

public sealed class UpsertTemplateDesignValidatorTests
{
    private readonly UpsertTemplateDesignValidator validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_ShouldSucceed()
    {
        UpsertTemplateDesignCommand command = new(
            TemplateId: "template-1",
            Name: "Template Name",
            Width: 1200,
            Height: 800,
            BackgroundColor: "#FFFFFF",
            ElementsJson: "[]",
            DefaultsJson: "{}");

        var result = this.validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenCommandHasInvalidValues_ShouldFail()
    {
        UpsertTemplateDesignCommand command = new(
            TemplateId: string.Empty,
            Name: string.Empty,
            Width: 0,
            Height: -1,
            BackgroundColor: string.Empty,
            ElementsJson: string.Empty,
            DefaultsJson: "{}");

        var result = this.validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(6);
    }
}
