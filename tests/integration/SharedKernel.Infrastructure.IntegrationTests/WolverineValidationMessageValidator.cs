using FluentValidation;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>FluentValidation rules applied by Wolverine's message middleware.</summary>
public sealed class WolverineValidationMessageValidator : AbstractValidator<WolverineValidationMessage>
{
    /// <summary>Initializes a new instance of the <see cref="WolverineValidationMessageValidator"/> class.</summary>
    public WolverineValidationMessageValidator()
    {
        _ = RuleFor(message => message.Name).NotEmpty().WithMessage("Name is required.");
    }
}
