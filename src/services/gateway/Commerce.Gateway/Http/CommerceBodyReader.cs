using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http;

namespace Teck.Cloud.Commerce.Gateway.Http;

/// <summary>Reads only generated request bodies and rejects schema deviations as invalid_request.</summary>
public static class CommerceBodyReader
{
    /// <summary>Deserializes one bounded JSON request body with unmapped members rejected.</summary>
    /// <typeparam name="T">The generated request body.</typeparam>
    /// <param name="request">The current request.</param>
    /// <param name="json">Generated metadata for the body type.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Success only for a present, schema-valid object.</returns>
    public static Task<(bool Ok, T? Body)> TryReadAsync<T>(HttpRequest request, JsonTypeInfo<T> json, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(json);
        return TryReadCoreAsync(request, json, cancellationToken);
    }

    /// <summary>Reads exactly one nonempty header value.</summary>
    /// <param name="request">The current request.</param>
    /// <param name="name">The header name.</param>
    /// <returns>The single value, or null when absent or repeated.</returns>
    public static string? SingleHeader(HttpRequest request, string name)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var values = request.Headers[name];
        return values.Count == 1 ? values[0] : null;
    }

    private static async Task<(bool Ok, T? Body)> TryReadCoreAsync<T>(HttpRequest request, JsonTypeInfo<T> json, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            var body = await JsonSerializer.DeserializeAsync(request.Body, json, cancellationToken).ConfigureAwait(false);
            return (body is not null, body);
        }
        catch (JsonException)
        {
            return (false, null);
        }
        catch (IOException)
        {
            return (false, null);
        }
    }
}
