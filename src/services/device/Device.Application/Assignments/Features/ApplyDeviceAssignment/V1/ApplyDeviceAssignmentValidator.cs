// <copyright file="ApplyDeviceAssignmentValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Device.Application.Assignments.Features.ApplyDeviceAssignment.V1;

/// <summary>
/// Validates <see cref="ApplyDeviceAssignmentRequest"/>.
/// </summary>
public sealed class ApplyDeviceAssignmentValidator : AbstractValidator<ApplyDeviceAssignmentRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplyDeviceAssignmentValidator"/> class.
    /// </summary>
    public ApplyDeviceAssignmentValidator()
    {
        RuleFor(request => request.DeviceId)
            .NotEmpty();

        RuleFor(request => request.LocationNodeId)
            .NotEmpty();

        RuleFor(request => request.Zones)
            .NotNull()
            .NotEmpty()
            .Must(zones => zones is not null && zones.Count is >= 1 and <= 3)
            .WithMessage("Zones must contain between 1 and 3 items.");

        RuleFor(request => request.Zones)
            .Must(zones => zones is not null && zones.Select(zone => zone.ZoneIndex).Distinct().Count() == zones.Count)
            .WithMessage("ZoneIndex values must be unique.");

        RuleForEach(request => request.Zones)
            .ChildRules(zone =>
            {
                zone.RuleFor(item => item.ZoneIndex)
                    .InclusiveBetween(1, 3);

                zone.RuleFor(item => item.ProductId)
                    .NotEmpty();
            });
    }
}
