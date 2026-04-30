// <copyright file="PatchCurrentTenantProfileValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Customer.Application.Tenants.Features.PatchCurrentTenantProfile.V1;

/// <summary>
/// Validator for current tenant profile patch requests.
/// </summary>
public sealed class PatchCurrentTenantProfileValidator : AbstractValidator<PatchCurrentTenantProfileRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PatchCurrentTenantProfileValidator"/> class.
    /// </summary>
    public PatchCurrentTenantProfileValidator()
    {
        this.RuleFor(request => request)
            .Must(request => !string.IsNullOrWhiteSpace(request.Name) || !string.IsNullOrWhiteSpace(request.Plan))
            .WithMessage("At least one profile field must be provided");

        this.When(request => request.Name is not null, () =>
        {
            this.RuleFor(request => request.Name)
                .NotEmpty();
        });

        this.When(request => request.Plan is not null, () =>
        {
            this.RuleFor(request => request.Plan)
                .NotEmpty();
        });
    }
}
