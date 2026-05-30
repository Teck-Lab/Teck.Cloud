// <copyright file="CreateLicenseValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Customer.Application.Licenses.Features.CreateLicense.V1;

/// <summary>
/// Validator for <see cref="CreateLicenseCommand"/>.
/// </summary>
public sealed class CreateLicenseValidator : AbstractValidator<CreateLicenseCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateLicenseValidator"/> class.
    /// </summary>
    public CreateLicenseValidator()
    {
        this.RuleFor(command => command.TenantId).NotEmpty().WithMessage("Tenant ID is required.");
        this.RuleFor(command => command.Plan).NotEmpty().WithMessage("Plan is required.");
        this.RuleFor(command => command.PaymentScope).NotEmpty().WithMessage("Payment scope is required.");
    }
}
