using ErrorOr;

namespace Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGenerations;

/// <summary>Stable generation-fence invariant failures.</summary>
public static class LicenseGenerationErrors
{
    /// <summary>Gets the validation error for a missing or malformed tenant, order or subscription identifier.</summary>
    public static Error InvalidBinding { get; } = Error.Validation("invalid_request", "A UUID tenant, order and subscription binding is required.");

    /// <summary>Gets the failure reported when the signed-license revision space is exhausted.</summary>
    public static Error RevisionExhausted { get; } = Error.Failure("dependency_unavailable", "The license revision space is exhausted.");
}
