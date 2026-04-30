// <copyright file="UpdateTenantProfileValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Customer.Application.Tenants.Features.UpdateTenantProfile.V1;

/// <summary>
/// Validator for tenant profile patch requests.
/// </summary>
public sealed class UpdateTenantProfileValidator : AbstractValidator<UpdateTenantProfileRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTenantProfileValidator"/> class.
    /// </summary>
    public UpdateTenantProfileValidator()
    {
        this.RuleFor(request => request.Id)
            .NotEmpty();

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
