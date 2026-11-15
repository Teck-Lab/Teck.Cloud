using Wolverine;
using Wolverine.FluentValidation;

namespace SharedKernel.Infrastructure.Messaging;

/// <summary>Configures the shared Wolverine validation middleware.</summary>
public static class WolverineValidationExtensions
{
    /// <summary>Discovers application FluentValidation validators and validates messages before handlers run.</summary>
    /// <param name="options">The Wolverine options.</param>
    /// <returns>The configured Wolverine options.</returns>
    public static WolverineOptions UseFoundationValidation(this WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _ = options.UseFluentValidation();
        return options;
    }
}
