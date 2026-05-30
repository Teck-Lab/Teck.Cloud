// <copyright file="CreateDeviceLayoutValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Device.Application.DeviceLayouts.Features.CreateDeviceLayout.V1;

/// <summary>
/// Validates <see cref="CreateDeviceLayoutRequest"/>.
/// </summary>
public sealed class CreateDeviceLayoutValidator : AbstractValidator<CreateDeviceLayoutRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateDeviceLayoutValidator"/> class.
    /// </summary>
    public CreateDeviceLayoutValidator()
    {
        RuleFor(request => request.DeviceDefinitionId)
            .NotEmpty();

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.MaxZoneCount)
            .GreaterThan(0);
    }
}
