// <copyright file="UpdateAccessPointStatusValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Device.Application.AccessPoints.Features.UpdateAccessPointStatus.V1;

/// <summary>
/// Validates <see cref="UpdateAccessPointStatusRequest"/>.
/// </summary>
public sealed class UpdateAccessPointStatusValidator : AbstractValidator<UpdateAccessPointStatusRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAccessPointStatusValidator"/> class.
    /// </summary>
    public UpdateAccessPointStatusValidator()
    {
        RuleFor(request => request.Serial)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Status)
            .NotEmpty()
            .MaximumLength(50);
    }
}
