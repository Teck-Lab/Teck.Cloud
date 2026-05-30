// <copyright file="SubmitRenderJobValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;
using Microsoft.Extensions.Options;

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1;

/// <summary>
/// Validates render requests for format support, element constraints, and template safety limits.
/// </summary>
public sealed class SubmitRenderJobValidator : AbstractValidator<SubmitRenderJobRequest>
{
    private static readonly string[] AllowedOutputTypes = ["png", "jpeg", "jpg", "webp", "pam"];
    private static readonly string[] AllowedTemplateElementTypes = ["text", "barcode", "code128", "qrcode", "datamatrix", "pdf417", "rectangle"];
    private const int MinDisplayColors = 2;
    private const int MaxDisplayColors = 7;
    private const int MinCanvasSize = 1;
    private const int MaxCanvasSize = 8192;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubmitRenderJobValidator"/> class.
    /// </summary>
    /// <param name="renderOptions">Render processing options used to enforce request limits.</param>
    public SubmitRenderJobValidator(IOptions<RenderProcessingOptions> renderOptions)
    {
        ArgumentNullException.ThrowIfNull(renderOptions);

        RenderProcessingOptions options = renderOptions.Value;
        int maxCanvasPixels = Math.Max(1, options.MaxCanvasPixels);
        int maxTemplateElements = Math.Max(1, options.MaxTemplateElements);

        RuleFor(request => request.DisplayId)
            .NotEmpty();

        RuleFor(request => request.OutputType)
            .NotEmpty()
            .Must(type => AllowedOutputTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
            .WithMessage("OutputType must be one of: png, jpeg, jpg, webp, pam.");

        RuleFor(request => request.PaletteColors)
            .NotNull()
            .Must(colors => colors.Count == 0 || (colors.Count >= MinDisplayColors && colors.Count <= MaxDisplayColors))
            .WithMessage($"PaletteColors must contain between {MinDisplayColors} and {MaxDisplayColors} colors when provided.")
            .Must(colors => colors.Count == colors.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            .WithMessage("PaletteColors must not contain duplicate color values.");

        RuleForEach(request => request.PaletteColors)
            .Matches("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$")
            .WithMessage("Palette color must be a hex value like #RRGGBB or #AARRGGBB.");

        RuleFor(request => request.Data)
            .NotNull();

        RuleForEach(request => request.Data)
            .Must(item => !string.IsNullOrWhiteSpace(item.Key))
            .WithMessage("Data keys must not be empty.");

        RuleFor(request => request.Template)
            .NotNull();

        RuleFor(request => request.Template.Width)
            .InclusiveBetween(MinCanvasSize, MaxCanvasSize);

        RuleFor(request => request.Template.Height)
            .InclusiveBetween(MinCanvasSize, MaxCanvasSize);

        RuleFor(request => request.Template)
            .Must(template => ((long)template.Width * template.Height) <= maxCanvasPixels)
            .WithMessage($"Template canvas area must not exceed {maxCanvasPixels} pixels.");

        RuleFor(request => request.Template.BackgroundColor)
            .Matches("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$")
            .WithMessage("Template background color must be a hex value like #RRGGBB or #AARRGGBB.");

        RuleFor(request => request.Template.Elements)
            .NotNull()
            .Must(elements => elements.Count > 0 && elements.Count <= maxTemplateElements)
            .WithMessage($"Template must contain between 1 and {maxTemplateElements} elements.");

        RuleForEach(request => request.Template.Elements)
            .ChildRules(element =>
            {
                element.RuleFor(item => item.Type)
                    .NotEmpty()
                    .Must(type => AllowedTemplateElementTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
                    .WithMessage("Template element type must be one of: text, barcode, code128, qrcode, datamatrix, pdf417, rectangle.");

                element.RuleFor(item => item.Left)
                    .GreaterThanOrEqualTo(0);

                element.RuleFor(item => item.Top)
                    .GreaterThanOrEqualTo(0);

                element.RuleFor(item => item.Width)
                    .GreaterThan(0)
                    .When(item => RequiresSizedElement(item.Type));

                element.RuleFor(item => item.Height)
                    .GreaterThan(0)
                    .When(item => RequiresSizedElement(item.Type));

                element.RuleFor(item => item.FontSize)
                    .GreaterThan(0)
                    .When(item => item.Type.Equals("text", StringComparison.OrdinalIgnoreCase));

                element.RuleFor(item => item.StrokeWidth)
                    .GreaterThanOrEqualTo(0);

                element.RuleFor(item => item.CornerRadius)
                    .GreaterThanOrEqualTo(0);

                element.RuleFor(item => item.HorizontalAlign)
                    .Must(align => IsEmptyOrOneOf(align, "left", "center", "right"))
                    .WithMessage("HorizontalAlign must be left, center, or right when provided.");

                element.RuleFor(item => item.ForegroundColor)
                    .Must(IsValidOptionalColor)
                    .WithMessage("ForegroundColor must be a hex value like #RRGGBB or #AARRGGBB when provided.");

                element.RuleFor(item => item.BackgroundColor)
                    .Must(IsValidOptionalColor)
                    .WithMessage("BackgroundColor must be a hex value like #RRGGBB or #AARRGGBB when provided.");

                element.RuleFor(item => item)
                    .Must(HasValueSourceWhenRequired)
                    .WithMessage("Text and barcode elements require either Value or Binding.");
            });
    }

    private static bool RequiresSizedElement(string type)
    {
        return type.Equals("barcode", StringComparison.OrdinalIgnoreCase)
            || type.Equals("code128", StringComparison.OrdinalIgnoreCase)
            || type.Equals("qrcode", StringComparison.OrdinalIgnoreCase)
            || type.Equals("datamatrix", StringComparison.OrdinalIgnoreCase)
            || type.Equals("pdf417", StringComparison.OrdinalIgnoreCase)
            || type.Equals("rectangle", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasValueSourceWhenRequired(SubmitRenderJobTemplateElementRequest element)
    {
        bool needsValue = element.Type.Equals("text", StringComparison.OrdinalIgnoreCase)
            || element.Type.Equals("barcode", StringComparison.OrdinalIgnoreCase)
            || element.Type.Equals("code128", StringComparison.OrdinalIgnoreCase)
            || element.Type.Equals("qrcode", StringComparison.OrdinalIgnoreCase)
            || element.Type.Equals("datamatrix", StringComparison.OrdinalIgnoreCase)
            || element.Type.Equals("pdf417", StringComparison.OrdinalIgnoreCase);

        if (!needsValue)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(element.Value) || !string.IsNullOrWhiteSpace(element.Binding);
    }

    private static bool IsValidOptionalColor(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            || System.Text.RegularExpressions.Regex.IsMatch(value, "^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$");
    }

    private static bool IsEmptyOrOneOf(string value, params string[] allowed)
    {
        return string.IsNullOrWhiteSpace(value) || allowed.Contains(value, StringComparer.OrdinalIgnoreCase);
    }
}
