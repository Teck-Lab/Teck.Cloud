using FastEndpoints;
using FluentValidation;

namespace SharedKernel.Api.TestHost;

/// <summary>Rejects an empty request name before the endpoint handler runs.</summary>
public sealed class ValidationProbeValidator : Validator<ValidationProbeRequest>
{
    /// <summary>Initializes a new instance of the <see cref="ValidationProbeValidator"/> class.</summary>
    public ValidationProbeValidator()
    {
        _ = RuleFor(request => request.Name).NotEmpty().WithMessage("Name is required.");
    }
}
