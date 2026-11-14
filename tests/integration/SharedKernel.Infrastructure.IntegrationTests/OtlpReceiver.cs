using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace SharedKernel.Infrastructure.IntegrationTests;

internal sealed class OtlpReceiver : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _pump;

    public OtlpReceiver()
    {
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        Address = new UriBuilder("http", "127.0.0.1", port).Uri;
        _listener.Prefixes.Add(Address.AbsoluteUri);
        _listener.Start();
        _pump = ReceiveAsync();
    }

    public Uri Address { get; }

    public ConcurrentQueue<ExportRequest> Requests { get; } = new();

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        await _pump.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
        _listener.Close();
        _stop.Dispose();
    }

    private async Task ReceiveAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync().WaitAsync(_stop.Token).ConfigureAwait(false);
                var body = new MemoryStream();
                await using var disposeBody = body.ConfigureAwait(false);
                await context.Request.InputStream.CopyToAsync(body, _stop.Token).ConfigureAwait(false);
                Requests.Enqueue(new ExportRequest(context.Request.Url!.AbsolutePath, body.ToArray(), context.Request.Headers["x-test-otel"]));
                context.Response.ContentType = "application/x-protobuf";
                context.Response.StatusCode = 200;
                context.Response.ContentLength64 = 0;
                context.Response.Close();
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // Expected when the fixture stops accepting exports.
        }
        catch (HttpListenerException) when (_stop.IsCancellationRequested)
        {
            // HttpListener.Stop interrupts an outstanding accept.
        }
    }

    internal sealed record ExportRequest(string Path, byte[] Body, string? Header);
}
