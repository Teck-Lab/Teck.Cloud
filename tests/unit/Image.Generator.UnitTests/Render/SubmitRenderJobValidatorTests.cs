using Image.Generator.Application.Render.Features.SubmitRenderJob.V1;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Image.Generator.UnitTests.Render;

public sealed class SubmitRenderJobValidatorTests
{
    private readonly SubmitRenderJobValidator sut;

    public SubmitRenderJobValidatorTests()
    {
        IOptions<RenderProcessingOptions> options = Options.Create(new RenderProcessingOptions
        {
            MaxCanvasPixels = 1000,
            MaxTemplateElements = 2,
        });

        sut = new SubmitRenderJobValidator(options);
    }

    [Fact]
    public void Validate_WhenRequiredFieldsMissing_ShouldReturnValidationErrors()
    {
        // Arrange
        SubmitRenderJobRequest request = new()
        {
            DisplayId = Guid.Empty,
            OutputType = string.Empty,
            Template = new SubmitRenderJobTemplateRequest
            {
                Width = 10,
                Height = 10,
                BackgroundColor = "#FFFFFF",
                Elements = [new SubmitRenderJobTemplateElementRequest { Type = "text", Value = "ok", FontSize = 10 }],
            },
            Data = new Dictionary<string, string>(),
        };

        // Act
        var result = sut.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Any(error => error.PropertyName == "DisplayId").ShouldBeTrue();
        result.Errors.Any(error => error.PropertyName == "OutputType").ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenDimensionsInvalid_ShouldReturnValidationErrors()
    {
        // Arrange
        SubmitRenderJobRequest request = BuildValidRequest() with
        {
            Template = BuildValidTemplate() with { Width = 0, Height = 0 },
        };

        // Act
        var result = sut.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Any(error => error.PropertyName == "Template.Width").ShouldBeTrue();
        result.Errors.Any(error => error.PropertyName == "Template.Height").ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenCanvasAreaExceedsLimit_ShouldReturnValidationError()
    {
        // Arrange
        SubmitRenderJobRequest request = BuildValidRequest() with
        {
            Template = BuildValidTemplate() with { Width = 100, Height = 100 },
        };

        // Act
        var result = sut.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Any(error => error.PropertyName == "Template").ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenPaletteHasInvalidValues_ShouldReturnValidationErrors()
    {
        // Arrange
        SubmitRenderJobRequest request = BuildValidRequest() with
        {
            PaletteColors = ["#FFFFFF", "#ffffff", "NOT_A_HEX"],
        };

        // Act
        var result = sut.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Any(error => error.PropertyName == "PaletteColors").ShouldBeTrue();
    }

    private static SubmitRenderJobRequest BuildValidRequest()
        => new()
        {
            DisplayId = Guid.NewGuid(),
            OutputType = "png",
            PaletteColors = [],
            Data = new Dictionary<string, string> { ["key"] = "value" },
            Template = BuildValidTemplate(),
        };

    private static SubmitRenderJobTemplateRequest BuildValidTemplate()
        => new()
        {
            Width = 20,
            Height = 20,
            BackgroundColor = "#FFFFFF",
            Elements =
            [
                new SubmitRenderJobTemplateElementRequest
                {
                    Type = "text",
                    Left = 0,
                    Top = 0,
                    Width = 10,
                    Height = 10,
                    Value = "hello",
                    FontFamily = "Arial",
                    FontSize = 10,
                    ForegroundColor = "#000000",
                },
            ],
        };
}
