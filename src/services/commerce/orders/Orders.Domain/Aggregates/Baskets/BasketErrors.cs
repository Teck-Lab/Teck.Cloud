using ErrorOr;

namespace Teck.Cloud.Commerce.Orders.Domain.Aggregates.Baskets;

/// <summary>Stable basket domain errors.</summary>
public static class BasketErrors
{
    /// <summary>Gets the stale revision error.</summary>
    public static Error StaleRevision => Error.Conflict("stale_revision", "The basket revision does not match the expected revision.");

    /// <summary>Gets the empty basket error.</summary>
    public static Error Empty => Error.Conflict("invalid_transition", "The basket has no selection.");

    /// <summary>Gets the unsupported option error.</summary>
    public static Error UnsupportedOption => Error.Validation("unsupported_option", "The selection contains an option that is not currently offered.");

    /// <summary>Gets the duplicate add-on error.</summary>
    public static Error DuplicateAddOn => Error.Validation("invalid_request", "Add-on codes must be unique.");

    /// <summary>Gets the invalid offer error.</summary>
    public static Error InvalidOffer => Error.Validation("invalid_request", "A valid offer is required.");

    /// <summary>Gets the scoped not-found error.</summary>
    public static Error NotFound => Error.NotFound("not_found", "The basket was not found.");
}
