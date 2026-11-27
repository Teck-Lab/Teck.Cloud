using Grpc.Core;
using Teck.Cloud.Commerce.Contracts;

namespace Teck.Cloud.Commerce.Gateway.Errors;

/// <summary>Interprets only complete, frozen commerce gRPC responses.</summary>
public static class CommerceGrpcCalls
{
    /// <summary>Awaits one unary dependency call, rejecting success trailers and untranslatable failures.</summary>
    /// <typeparam name="T">The response message.</typeparam>
    /// <param name="call">The started unary call.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The successful response.</returns>
    /// <exception cref="RpcException">A valid frozen non-OK dependency outcome.</exception>
    /// <exception cref="CommerceDependencyException">A malformed, transport-failed or otherwise fail-closed outcome.</exception>
    public static Task<T> UnaryAsync<T>(AsyncUnaryCall<T> call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        return UnaryCoreAsync(call, cancellationToken);
    }

    /// <summary>Checks whether a trailer collection contains the reservation key at all.</summary>
    /// <param name="trailers">Completed call trailers.</param>
    /// <returns>True when the error trailer is present, including duplicates.</returns>
    public static bool HasErrorTrailer(Metadata trailers)
    {
        ArgumentNullException.ThrowIfNull(trailers);
        return trailers.Any(entry => string.Equals(entry.Key, CommerceRpc.ErrorTrailer, StringComparison.Ordinal));
    }

    private static async Task<T> UnaryCoreAsync<T>(AsyncUnaryCall<T> call, CancellationToken cancellationToken)
    {
        T response;
        bool hasErrorTrailer;
        try
        {
            var responseTask = call.ResponseAsync;
            response = await responseTask.ConfigureAwait(false);
            hasErrorTrailer = HasErrorTrailer(call.GetTrailers());
        }
        catch (RpcException error)
        {
            var code = CommerceRpc.ErrorCode(error);
            if (string.Equals(code, CommerceErrorCodes.DependencyUnavailable, StringComparison.Ordinal))
            {
                throw new CommerceDependencyException("The dependency failure is unavailable or malformed.", error);
            }

            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (error is not CommerceDependencyException)
        {
            throw new CommerceDependencyException("The dependency call failed outside a valid commerce response.", error);
        }

        return hasErrorTrailer
            ? throw new CommerceDependencyException("The dependency returned a success response with an error trailer.")
            : response;
    }
}
