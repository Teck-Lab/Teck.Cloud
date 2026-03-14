// <copyright file="CreateTenantValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Customer.Api.Endpoints.V1.Tenants.CreateTenant;

/// <summary>
/// Validator for CreateTenantRequest.
/// </summary>
internal class CreateTenantValidator : AbstractValidator<CreateTenantRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTenantValidator"/> class.
    /// </summary>
    public CreateTenantValidator()
    {
        this.RuleFor(request => request.Identifier)
            .NotEmpty().WithMessage("Identifier is required")
            .MaximumLength(100).WithMessage("Identifier must not exceed 100 characters");

        this.RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(255).WithMessage("Name must not exceed 255 characters");

        this.RuleFor(request => request.Plan)
            .NotEmpty().WithMessage("Plan is required");

        this.RuleFor(request => request.DatabaseStrategy)
            .NotEmpty().WithMessage("DatabaseStrategy is required")
            .Must(strategy => strategy is "Shared" or "Dedicated" or "External")
            .WithMessage("DatabaseStrategy must be Shared, Dedicated, or External");
    }
}
