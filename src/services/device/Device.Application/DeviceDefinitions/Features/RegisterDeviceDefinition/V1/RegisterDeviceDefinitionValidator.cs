// <copyright file="RegisterDeviceDefinitionValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;
using SharedKernel.Core.Devices;

namespace Device.Application.DeviceDefinitions.Features.RegisterDeviceDefinition.V1;

/// <summary>
/// Validates <see cref="RegisterDeviceDefinitionRequest"/>.
/// </summary>
public sealed class RegisterDeviceDefinitionValidator : AbstractValidator<RegisterDeviceDefinitionRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterDeviceDefinitionValidator"/> class.
    /// </summary>
    public RegisterDeviceDefinitionValidator()
    {
        RuleFor(request => request.ModelId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.EslProvider)
            .NotEmpty()
            .Must(value => EslProvider.TryFromName(value, false, out _))
            .WithMessage("EslProvider must be a valid value: Unknown, Hanshow, SoluM.");

        RuleFor(request => request.WidthPx)
            .GreaterThan(0)
            .When(request => request.WidthPx.HasValue);

        RuleFor(request => request.HeightPx)
            .GreaterThan(0)
            .When(request => request.HeightPx.HasValue);
    }
}
