using System.Collections.Frozen;
using Grpc.Core;

namespace Teck.Cloud.Commerce.Contracts;

/// <summary>Creates and validates the frozen commerce gRPC error wire contract.</summary>
public static class CommerceRpc
{
    /// <summary>The single trailer carrying a lowercase stable error code.</summary>
    public const string ErrorTrailer = "teck-commerce-error-code";

    private static readonly FrozenDictionary<string, StatusCode> StatusByCode =
        new Dictionary<string, StatusCode>(StringComparer.Ordinal)
        {
            [CommerceErrorCodes.InvalidRequest] = StatusCode.InvalidArgument,
            [CommerceErrorCodes.Unauthenticated] = StatusCode.Unauthenticated,
            [CommerceErrorCodes.Forbidden] = StatusCode.PermissionDenied,
            [CommerceErrorCodes.NotFound] = StatusCode.NotFound,
            [CommerceErrorCodes.IdempotencyConflict] = StatusCode.AlreadyExists,
            [CommerceErrorCodes.StaleRevision] = StatusCode.Aborted,
            [CommerceErrorCodes.InvalidTransition] = StatusCode.FailedPrecondition,
            [CommerceErrorCodes.UnsupportedOption] = StatusCode.FailedPrecondition,
            [CommerceErrorCodes.DependencyUnavailable] = StatusCode.Unavailable,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Builds a failure whose trailer and status are the frozen pair for the code.</summary>
    /// <param name="code">One of the <see cref="CommerceErrorCodes"/> constants.</param>
    /// <returns>The exception a commerce handler throws.</returns>
    /// <exception cref="ArgumentException">The code is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The code is not a frozen stable code.</exception>
    public static RpcException Error(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return !StatusByCode.TryGetValue(code, out var status)
            ? throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown commerce error code.")
            : new RpcException(new Status(status, string.Empty), new Metadata { { ErrorTrailer, code } });
    }

    /// <summary>Returns the validated stable code, or the fail-closed dependency code.</summary>
    /// <param name="error">A non-OK dependency failure.</param>
    /// <returns>The validated stable code, otherwise <see cref="CommerceErrorCodes.DependencyUnavailable"/>.</returns>
    public static string ErrorCode(RpcException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return ReadErrorCode(error.StatusCode, error.Trailers) ?? CommerceErrorCodes.DependencyUnavailable;
    }

    /// <summary>Reads and validates a response trailer against the observed status.</summary>
    /// <param name="status">The observed gRPC status.</param>
    /// <param name="trailers">The response trailers, if any.</param>
    /// <returns>Null only for a clean OK response without the error trailer; otherwise the validated code or <see cref="CommerceErrorCodes.DependencyUnavailable"/>.</returns>
    public static string? ReadErrorCode(StatusCode status, Metadata? trailers)
    {
        var entries = trailers?.Where(entry => string.Equals(entry.Key, ErrorTrailer, StringComparison.Ordinal)).ToArray()
            ?? [];
        return (status, entries.Length) switch
        {
            (StatusCode.OK, 0) => null,
            (StatusCode.OK, _) => CommerceErrorCodes.DependencyUnavailable,
            (_, 1) when StatusByCode.TryGetValue(entries[0].Value, out var expected) && expected == status => entries[0].Value,
            _ => CommerceErrorCodes.DependencyUnavailable,
        };
    }
}
