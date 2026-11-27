using System.Net.Http.Headers;
using System.Text;
using Commerce.IntegrationTests.Infrastructure;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Owns the fake HTTP/2 backends and the real customer gateway process for one scenario.</summary>
internal sealed class GatewayHarness : IAsyncDisposable
{
    internal required FakeCommerceBackend Backend { get; init; }

    internal required CustomerGatewayProcess Gateway { get; init; }

    internal CommerceCallRecorder Recorder => Backend.Recorder;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await Gateway.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await Backend.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal static async Task<GatewayHarness> StartAsync(
        FakeCommerceScript script,
        CommerceKeycloakFixture keycloak,
        CancellationToken cancellationToken)
    {
        FakeCommerceBackend? backend = null;
        CustomerGatewayProcess? gateway = null;
        try
        {
            backend = await FakeCommerceBackend.StartAsync(script, cancellationToken).ConfigureAwait(false);
            gateway = await CustomerGatewayProcess.StartAsync(backend, keycloak, cancellationToken).ConfigureAwait(false);
            var harness = new GatewayHarness { Backend = backend, Gateway = gateway };
            gateway = null;
            backend = null;
            return harness;
        }
        finally
        {
            try
            {
                if (gateway is not null)
                {
                    await gateway.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                if (backend is not null)
                {
                    await backend.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }

    internal async Task<HttpResponseMessage> SendAsync(
        GatewaySurface surface,
        string accessToken,
        CancellationToken cancellationToken,
        Guid? orderId = null,
        Action<HttpRequestMessage>? customize = null)
    {
        using var request = CreateRequest(surface, accessToken, orderId);
        customize?.Invoke(request);
        return await Gateway.Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static HttpRequestMessage CreateRequest(GatewaySurface surface, string accessToken, Guid? orderId)
    {
        var target = orderId ?? CommerceFixtures.OrderA;
        var request = surface switch
        {
            GatewaySurface.Detail => new HttpRequestMessage(HttpMethod.Get, "/v1/commerce/orders/" + target.ToString("D")),
            GatewaySurface.List => new HttpRequestMessage(
                HttpMethod.Get,
                "/v1/commerce/orders?limit=100&cursor=" + Uri.EscapeDataString(CommerceFixtures.Cursor)),
            GatewaySurface.Replay => new HttpRequestMessage(HttpMethod.Post, "/v1/commerce/orders")
            {
                Content = new StringContent(
                    "{\"basketRevision\":" + CommerceFixtures.ReplayBasketRevision.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}",
                    Encoding.UTF8,
                    "application/json"),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(surface)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (surface == GatewaySurface.Replay)
        {
            request.Headers.Add("Idempotency-Key", CommerceFixtures.IdempotencyKey);
        }

        return request;
    }
}
