// <copyright file="AddDisplaysValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Device.Application.Displays.Features.AddDisplays.V1;

/// <summary>
/// Validates <see cref="AddDisplaysRequest"/>.
/// </summary>
public sealed class AddDisplaysValidator : AbstractValidator<AddDisplaysRequest>
{
    private static readonly System.Text.RegularExpressions.Regex ShortSerialPattern =
        new(
            @"^[0-9A-Fa-f]{2}-[0-9A-Fa-f]{2}-[0-9A-Fa-f]{2}-[0-9A-Fa-f]{2}$",
            System.Text.RegularExpressions.RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Initializes a new instance of the <see cref="AddDisplaysValidator"/> class.
    /// </summary>
    public AddDisplaysValidator()
    {
        RuleFor(request => request.LocationNodeId)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Displays)
            .NotEmpty()
            .WithMessage("At least one display must be provided.")
            .Must(items => items.Count <= 500)
            .WithMessage("A maximum of 500 displays can be added per request.");

        RuleForEach(request => request.Displays).ChildRules(item =>
        {
            item.RuleFor(display => display.ShortSerial)
                .NotEmpty()
                .Matches(ShortSerialPattern)
                .WithMessage("Short serial must match the pattern XX-XX-XX-XX (hex bytes).");
        });
    }
}
