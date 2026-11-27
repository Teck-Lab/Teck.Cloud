using ErrorOr;

namespace Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;

/// <summary>Stable license-grant invariant failures.</summary>
public static class LicenseGrantErrors
{
    /// <summary>Gets the validation error for a missing or malformed tenant, order or subscription identifier.</summary>
    public static Error InvalidBinding { get; } = Error.Validation("invalid_request", "A UUID tenant, order and subscription binding is required.");

    /// <summary>Gets the validation error for a desired generation or license revision outside the supported range.</summary>
    public static Error InvalidRevision { get; } = Error.Validation("invalid_request", "Desired and license revisions must be positive 32-bit integers.");

    /// <summary>Gets the validation error for a requested tier that is not a supported license tier.</summary>
    public static Error InvalidTier { get; } = Error.Validation("invalid_request", "Only shared and dedicated license tiers are supported.");

    /// <summary>Gets the validation error for a malformed permission snapshot or one that exceeds its bounds.</summary>
    public static Error InvalidAddOns { get; } = Error.Validation("invalid_request", "Add-on codes must be unique, canonical, bounded identifiers.");

    /// <summary>Gets the validation error for a license expiry that is not a future UTC instant.</summary>
    public static Error InvalidExpiry { get; } = Error.Validation("invalid_request", "A license requires a future UTC expiry.");

    /// <summary>Gets the validation error for an invalid signed document, key identifier or key metadata.</summary>
    public static Error InvalidDocument { get; } = Error.Validation("invalid_request", "A signed document and bounded key identifier are required.");

    /// <summary>Gets the validation error for an unidentified originating integration message.</summary>
    public static Error InvalidMessage { get; } = Error.Validation("invalid_request", "A source message identifier is required.");
}
