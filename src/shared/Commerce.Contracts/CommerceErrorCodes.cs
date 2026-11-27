namespace Teck.Cloud.Commerce.Contracts;

/// <summary>The frozen v1 commerce error codes carried in <see cref="CommerceRpc.ErrorTrailer"/>.</summary>
public static class CommerceErrorCodes
{
    /// <summary>Schema or semantic validation failure.</summary>
    public const string InvalidRequest = "invalid_request";

    /// <summary>Missing or invalid credentials.</summary>
    public const string Unauthenticated = "unauthenticated";

    /// <summary>Authenticated but not permitted.</summary>
    public const string Forbidden = "forbidden";

    /// <summary>Absent or another tenant's resource.</summary>
    public const string NotFound = "not_found";

    /// <summary>Same idempotency key with a changed intent.</summary>
    public const string IdempotencyConflict = "idempotency_conflict";

    /// <summary>Optimistic concurrency failure.</summary>
    public const string StaleRevision = "stale_revision";

    /// <summary>Illegal domain transition.</summary>
    public const string InvalidTransition = "invalid_transition";

    /// <summary>An offer or add-on is not currently offered.</summary>
    public const string UnsupportedOption = "unsupported_option";

    /// <summary>A retryable dependency failure.</summary>
    public const string DependencyUnavailable = "dependency_unavailable";
}
