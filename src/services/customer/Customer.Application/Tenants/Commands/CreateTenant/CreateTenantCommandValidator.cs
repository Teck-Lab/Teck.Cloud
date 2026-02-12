using FluentValidation;
using SharedKernel.Core.Pricing;

namespace Customer.Application.Tenants.Commands.CreateTenant;

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
        RuleFor(command => command.Identifier)
            .NotEmpty().WithMessage("Identifier is required")
            .MaximumLength(100).WithMessage("Identifier must not exceed 100 characters")
            .Matches("^[a-z0-9-]+$").WithMessage("Identifier must contain only lowercase letters, numbers, and hyphens");

        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(command => command.Plan)
            .NotEmpty().WithMessage("Plan is required")
            .MaximumLength(50).WithMessage("Plan must not exceed 50 characters");

        RuleFor(command => command.DatabaseStrategy)
            .NotNull().WithMessage("DatabaseStrategy is required")
            .Must(strategy => strategy != DatabaseStrategy.None).WithMessage("DatabaseStrategy cannot be None");

        RuleFor(command => command.DatabaseProvider)
            .NotNull().WithMessage("DatabaseProvider is required")
            .Must(provider => provider != DatabaseProvider.None).WithMessage("DatabaseProvider cannot be None");

        RuleFor(command => command.CustomCredentials)
            .NotNull().WithMessage("CustomCredentials are required when using External database strategy")
            .When(command => command.DatabaseStrategy == DatabaseStrategy.External);
    }
}
