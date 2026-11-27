using System.Net;
using System.Text.Json;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Buffered HTTP response used by the customer gateway assertions.</summary>
internal sealed class CommerceHttpResponse : IDisposable
{
    private CommerceHttpResponse(HttpStatusCode status, string? mediaType, string body, JsonDocument json)
    {
        Status = status;
        MediaType = mediaType;
        Body = body;
        Json = json;
    }

    internal HttpStatusCode Status { get; }

    internal string? MediaType { get; }

    internal string Body { get; }

    internal JsonDocument Json { get; }

    internal JsonElement Root => Json.RootElement;

    /// <inheritdoc/>
    public void Dispose() => Json.Dispose();

    internal static async Task<CommerceHttpResponse> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            return new CommerceHttpResponse(response.StatusCode, mediaType, body, JsonDocument.Parse(body));
        }
    }
}
