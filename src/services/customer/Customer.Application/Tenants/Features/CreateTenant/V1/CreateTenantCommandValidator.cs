// <copyright file="CreateTenantCommandValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;
using SharedKernel.Core.Pricing;

namespace Customer.Application.Tenants.Features.CreateTenant.V1;

/// <summary>
/// Validator for CreateTenantCommand.
/// </summary>
public class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTenantCommandValidator"/> class.
    /// </summary>
    public CreateTenantCommandValidator()
    {
        this.RuleFor(command => command.Identifier)
            .NotEmpty().WithMessage("Identifier is required")
            .MaximumLength(100).WithMessage("Identifier must not exceed 100 characters")
            .Matches("^[a-z0-9-]+$").WithMessage("Identifier must contain only lowercase letters, numbers, and hyphens");

        this.RuleFor(command => command.Profile.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        this.RuleFor(command => command.Profile.Plan)
            .NotEmpty().WithMessage("Plan is required")
            .MaximumLength(50).WithMessage("Plan must not exceed 50 characters");

        this.RuleFor(command => command.Database.DatabaseStrategy)
            .NotNull().WithMessage("DatabaseStrategy is required")
            .Must(strategy => strategy != DatabaseStrategy.None).WithMessage("DatabaseStrategy cannot be None");

        this.RuleFor(command => command.Database.DatabaseProvider)
            .NotNull().WithMessage("DatabaseProvider is required")
            .Must(provider => provider != DatabaseProvider.None).WithMessage("DatabaseProvider cannot be None");
    }
}
