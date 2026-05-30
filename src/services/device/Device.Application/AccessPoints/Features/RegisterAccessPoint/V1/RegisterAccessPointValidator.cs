// <copyright file="RegisterAccessPointValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Device.Application.AccessPoints.Features.RegisterAccessPoint.V1;

/// <summary>
/// Validates <see cref="RegisterAccessPointRequest"/>.
/// </summary>
public sealed class RegisterAccessPointValidator : AbstractValidator<RegisterAccessPointRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterAccessPointValidator"/> class.
    /// </summary>
    public RegisterAccessPointValidator()
    {
        RuleFor(request => request.SerialNumber)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Vendor)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(request => request.LocationNodeId)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.MaxCapacity)
            .GreaterThan(0);
    }
}
