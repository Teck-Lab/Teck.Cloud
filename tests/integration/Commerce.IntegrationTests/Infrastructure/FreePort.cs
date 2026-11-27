using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>Reserves an ephemeral loopback port for Kestrel listeners.</summary>
internal static class FreePort
{
    internal static int Next()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    internal static Uri Http(int port) => new(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}"));
}
