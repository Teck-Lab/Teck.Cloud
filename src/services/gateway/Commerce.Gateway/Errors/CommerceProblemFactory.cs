using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Models;

namespace Teck.Cloud.Commerce.Gateway.Errors;

/// <summary>Maps only frozen commerce codes to their published problem shapes.</summary>
public static class CommerceProblemFactory
{
    private static readonly Dictionary<string, (int Status, string Title, string Type, string Detail)> Problems = new(StringComparer.Ordinal)
    {
        [CommerceErrorCodes.InvalidRequest] = (400, "Request validation failed", "invalid-request", "The request body does not match the published schema."),
        [CommerceErrorCodes.Unauthenticated] = (401, "Authentication required", "unauthenticated", "A valid access token is required."),
        [CommerceErrorCodes.Forbidden] = (403, "Not permitted", "forbidden", "The principal is not permitted to perform this operation."),
        [CommerceErrorCodes.NotFound] = (404, "Resource not available", "not-found", "The resource does not exist or is not visible to this principal."),
        [CommerceErrorCodes.IdempotencyConflict] = (409, "Idempotency key conflict", "idempotency-conflict", "The idempotency key was already used with a different basket revision."),
        [CommerceErrorCodes.StaleRevision] = (409, "Revision conflict", "stale-revision", "The expected revision does not match the current revision."),
        [CommerceErrorCodes.InvalidTransition] = (409, "Order state does not allow this operation", "invalid-transition", "The current order state does not allow this transition."),
        [CommerceErrorCodes.UnsupportedOption] = (422, "Selection is not offered", "unsupported-option", "One or more offer or add-on codes are not currently offered."),
        [CommerceErrorCodes.DependencyUnavailable] = (503, "Dependency unavailable", "dependency-unavailable", "A required dependency is temporarily unavailable; retry with backoff."),
    };

    /// <summary>Builds the sanitized problem for a known code; unknown values fail closed as unavailable.</summary>
    /// <param name="code">Validated stable dependency code.</param>
    /// <param name="instance">Optional request path.</param>
    /// <returns>The published problem body.</returns>
    public static CommerceProblemView FromCode(string? code, string? instance = null)
    {
        var value = code is not null && Problems.TryGetValue(code, out var problem) ? problem : Problems[CommerceErrorCodes.DependencyUnavailable];
        return new CommerceProblemView
        {
            Type = "https://api.teck.cloud/problems/" + value.Type,
            Title = value.Title,
            Status = value.Status,
            Code = code is not null && Problems.ContainsKey(code) ? code : CommerceErrorCodes.DependencyUnavailable,
            Detail = value.Detail,
            Instance = instance,
        };
    }

    /// <summary>Builds a dependency-unavailable problem.</summary>
    /// <param name="instance">Optional request path.</param>
    /// <returns>The fail-closed problem body.</returns>
    public static CommerceProblemView DependencyUnavailable(string? instance = null) => FromCode(CommerceErrorCodes.DependencyUnavailable, instance);

    /// <summary>Gets the Retry-After value for a 503 response.</summary>
    /// <param name="status">The published HTTP status.</param>
    /// <returns>One second for retryable unavailability, otherwise zero.</returns>
    public static int RetryAfterSeconds(int status) => status == 503 ? 1 : 0;
}
