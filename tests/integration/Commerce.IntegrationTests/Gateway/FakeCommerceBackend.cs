using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Controllable HTTP/2 gRPC Orders and Licensing endpoint for the customer gateway matrix.</summary>
internal sealed class FakeCommerceBackend : IAsyncDisposable
{
    private readonly WebApplication _application;

    private FakeCommerceBackend(WebApplication application, Uri address, CommerceCallRecorder recorder)
    {
        _application = application;
        Address = address;
        Recorder = recorder;
    }

    internal Uri Address { get; }

    internal CommerceCallRecorder Recorder { get; }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await _application.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            await _application.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal static async Task<FakeCommerceBackend> StartAsync(FakeCommerceScript script, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        _ = builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
        _ = builder.Services.AddGrpc();
        _ = builder.Services.AddSingleton(script);
        _ = builder.Services.AddSingleton<CommerceCallRecorder>();
        var application = builder.Build();
        try
        {
            _ = application.MapGrpcService<FakeOrdersService>();
            _ = application.MapGrpcService<FakeLicensingService>();
            await application.StartAsync(cancellationToken).ConfigureAwait(false);
            return new FakeCommerceBackend(application, BoundAddress(application), application.Services.GetRequiredService<CommerceCallRecorder>());
        }
        catch
        {
            await application.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static Uri BoundAddress(WebApplication application)
    {
        var feature = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The fake commerce server exposes no bound address.");
        return new Uri(feature.Addresses.Single());
    }
}
