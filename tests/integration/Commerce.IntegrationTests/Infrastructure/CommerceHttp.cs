using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>Minimal JSON HTTP access with bounded timeouts.</summary>
internal sealed class CommerceHttp : IDisposable
{
    private readonly SocketsHttpHandler _handler = new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
    };

    internal CommerceHttp(Uri baseAddress)
    {
        Client = new HttpClient(_handler, disposeHandler: false)
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(30),
        };
    }

    internal HttpClient Client { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Client.Dispose();
        _handler.Dispose();
    }

    internal async Task<ApiResponse> SendAsync(
        HttpMethod method,
        string path,
        string? token,
        JsonNode? body,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (headers is not null)
        {
            foreach (var header in headers)
            {
                _ = request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        using var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new ApiResponse(response.StatusCode, text);
    }

    internal Task<ApiResponse> GetAsync(string path, string token, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, path, token, body: null, cancellationToken);

    internal Task<ApiResponse> PostAsync(string path, string token, JsonNode body, CancellationToken cancellationToken, IReadOnlyDictionary<string, string>? headers = null) =>
        SendAsync(HttpMethod.Post, path, token, body, cancellationToken, headers);

    internal Task<ApiResponse> PutAsync(string path, string token, JsonNode body, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, path, token, body, cancellationToken);

    internal async Task<ApiResponse> PostRawAsync(string path, string rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(rawBody, System.Text.Encoding.UTF8, "application/json"),
        };
        foreach (var header in headers)
        {
            _ = request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        using var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new ApiResponse(response.StatusCode, text);
    }
}
