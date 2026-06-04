// <copyright file="PreviewDeviceAssignmentValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Device.Application.Assignments.Features.PreviewDeviceAssignment.V1;

/// <summary>
/// Validates <see cref="PreviewDeviceAssignmentRequest"/>.
/// </summary>
public sealed class PreviewDeviceAssignmentValidator : AbstractValidator<PreviewDeviceAssignmentRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewDeviceAssignmentValidator"/> class.
    /// </summary>
    public PreviewDeviceAssignmentValidator()
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
