using System.Net;
using System.Net.Sockets;

namespace Teck.Cloud.AppHost;

/// <summary>Checks local prerequisites before Aspire starts resources.</summary>
internal static class StartupPreflight
{
    internal static void AddStartupPreflight(this IDistributedApplicationBuilder builder)
    {
        if (!builder.ExecutionContext.IsRunMode)
        {
            return;
        }

        _ = builder.Eventing.Subscribe<BeforeStartEvent>((_, cancellationToken) => CheckAsync(
            builder.Configuration["Profile"] ?? "development",
            builder.Configuration["ASPIRE_CONTAINER_RUNTIME"] ?? "docker",
            builder.Configuration["TECK_KIND_MODE"],
            builder.Configuration["Parameters:redis-advertised-host"],
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS(),
            PreflightCommand.RunAsync,
            Dns.GetHostAddressesAsync,
            cancellationToken));
    }

    internal static async Task CheckAsync(
        string profile,
        string runtime,
        string? mode,
        string? advertisedHost,
        bool requiresMachine,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        Func<string, CancellationToken, Task<IPAddress[]>> resolve,
        CancellationToken cancellationToken)
    {
        if (runtime is not ("docker" or "podman"))
        {
            throw new InvalidOperationException("ASPIRE_CONTAINER_RUNTIME must be docker or podman.");
        }

        if (profile is not ("development" or "sentinel" or "kind"))
        {
            throw new InvalidOperationException("Profile must be development, sentinel or kind.");
        }

        if (!string.Equals(profile, "kind", StringComparison.Ordinal))
        {
            await CheckAdvertisedHostAsync(advertisedHost, resolve, cancellationToken).ConfigureAwait(false);
        }

        _ = await run(runtime, ["info", "--format", "{{json .}}"], "The selected container engine is unavailable. Start it and repair its configured connection before starting Aspire.", cancellationToken).ConfigureAwait(false);
        if (requiresMachine && !string.Equals(profile, "kind", StringComparison.Ordinal) && string.Equals(runtime, "podman", StringComparison.Ordinal))
        {
            var machine = await run("podman", ["machine", "info", "--format", "{{.Host.CurrentMachine}}"], "Podman has no usable machine. Configure the machine selected for Aspire.", cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(machine))
            {
                throw new InvalidOperationException("Podman has no current machine. Select a running machine before starting Aspire.");
            }

            _ = await run("podman", ["machine", "ssh", machine, "true"], "Podman engine API is reachable, but its machine SSH path is unavailable. Aspire file copying needs this connection too; repair the selected Podman machine connection.", cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(profile, "kind", StringComparison.Ordinal))
        {
            _ = KindHostingExtensions.IsSandboxMode(mode);
            _ = await run("kind", ["version"], "The KIND profile requires kind on PATH.", cancellationToken).ConfigureAwait(false);
            _ = await run("kubectl", ["version", "--client=true"], "The KIND profile requires kubectl on PATH.", cancellationToken).ConfigureAwait(false);
            _ = await run("helm", ["version", "--short"], "The KIND profile requires Helm on PATH.", cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task CheckAdvertisedHostAsync(string? host, Func<string, CancellationToken, Task<IPAddress[]>> resolve, CancellationToken cancellationToken)
    {
        const string guidance = "Set Parameters:redis-advertised-host to an IPv4 address or DNS name reachable from both host applications and containers. Loopback, wildcard addresses, URLs and host:port values are not supported.";
        if (string.IsNullOrWhiteSpace(host) || Uri.CheckHostName(host) is not (UriHostNameType.IPv4 or UriHostNameType.Dns))
        {
            throw new InvalidOperationException(guidance);
        }

        IPAddress[] addresses;
        try
        {
            addresses = IPAddress.TryParse(host, out var address) ? [address] : await resolve(host, cancellationToken).WaitAsync(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException exception)
        {
            throw new InvalidOperationException("Redis Sentinel's advertised host cannot be resolved. " + guidance, exception);
        }

        if (addresses.Length == 0 || addresses.Any(address => IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any)) || !addresses.Any(address => address.AddressFamily == AddressFamily.InterNetwork))
        {
            throw new InvalidOperationException(guidance);
        }
    }
}
