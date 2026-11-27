using ErrorOr;

namespace Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;

/// <summary>Stable order domain errors.</summary>
public static class OrderErrors
{
    /// <summary>Gets the expected order revision is stale error.</summary>
    public static Error StaleRevision => Error.Conflict("stale_revision", "The order revision does not match the expected revision.");

    /// <summary>Gets the final-decision conflict error.</summary>
    public static Error DecisionFinal => Error.Conflict("invalid_transition", "The order decision is final.");

    /// <summary>Gets the paid-and-approved evidence requirement error.</summary>
    public static Error EvidenceNotAllowed => Error.Conflict("invalid_transition", "Placement evidence requires a paid and approved order.");

    /// <summary>Gets the invalid-detail error.</summary>
    public static Error InvalidDetail => Error.Validation("invalid_request", "The supplied detail is invalid.");

    /// <summary>Gets the unconfigured-placement error.</summary>
    public static Error UnknownPlacement => Error.Validation("invalid_request", "The placement is not configured.");

    /// <summary>Gets the scoped not-found error.</summary>
    public static Error NotFound => Error.NotFound("not_found", "The order was not found.");

    /// <summary>Gets the malformed-idempotency-key error.</summary>
    public static Error InvalidIdempotencyKey => Error.Validation("invalid_request", "The idempotency key must be a UUID.");

    /// <summary>Gets the same-key different-revision error.</summary>
    public static Error IdempotencyConflict => Error.Conflict("idempotency_conflict", "The idempotency key was used with a different basket revision.");

    /// <summary>Gets the already-consumed-revision error.</summary>
    public static Error StaleBasket => Error.Conflict("stale_revision", "The basket revision was already consumed.");

    /// <summary>Gets the not-payable lifecycle error.</summary>
    public static Error NotPayable => Error.Conflict("invalid_transition", "The order is not payable.");
}
