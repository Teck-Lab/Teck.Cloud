using FastEndpoints;
using FluentValidation;

namespace Customer.Api.Endpoints.V1.Tenants.CreateTenant;

/// <summary>
/// Validator for CreateTenantRequest.
/// </summary>
internal class CreateTenantValidator : Validator<CreateTenantRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTenantValidator"/> class.
    /// </summary>
    public CreateTenantValidator()
    {
        RuleFor(request => request.Identifier)
            .NotEmpty().WithMessage("Identifier is required")
            .MaximumLength(100).WithMessage("Identifier must not exceed 100 characters");

        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(255).WithMessage("Name must not exceed 255 characters");

        RuleFor(request => request.Plan)
            .NotEmpty().WithMessage("Plan is required");

        RuleFor(request => request.DatabaseStrategy)
            .NotEmpty().WithMessage("DatabaseStrategy is required")
            .Must(strategy => strategy is "Shared" or "Dedicated" or "External")
            .WithMessage("DatabaseStrategy must be Shared, Dedicated, or External");

        RuleFor(request => request.DatabaseProvider)
            .NotEmpty().WithMessage("DatabaseProvider is required")
            .Must(provider => provider is "PostgreSQL" or "SqlServer" or "MySQL")
            .WithMessage("DatabaseProvider must be PostgreSQL, SqlServer, or MySQL");

        RuleFor(request => request.CustomCredentials)
            .NotNull()
            .When(request => request.DatabaseStrategy == "External")
            .WithMessage("CustomCredentials are required for External database strategy");
    }
}
