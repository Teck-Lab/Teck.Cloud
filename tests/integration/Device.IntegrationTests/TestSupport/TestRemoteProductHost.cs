using System.Net;
using System.Net.Sockets;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Infrastructure;
using SharedKernel.Grpc.Contracts.Remote.V1.Products;

namespace Device.IntegrationTests.TestSupport;

internal sealed class TestRemoteProductHost : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly TestRemoteProductState state;

    private TestRemoteProductHost(WebApplication app, TestRemoteProductState state, string baseAddress)
    {
        this.app = app;
        this.state = state;
        this.BaseAddress = baseAddress;
    }

    public string BaseAddress { get; }

    public int CallCount => this.state.CallCount;

    public static async Task<TestRemoteProductHost> StartAsync(Guid productId, string productName, CancellationToken cancellationToken)
    {
        var state = new TestRemoteProductState(productId, productName);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });

        var port = GetFreeTcpPort();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.ConfigureInternalServiceTransport();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ConfigureEndpointDefaults(listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });
        builder.AddHandlerServer();
        builder.Services.AddRouting();
        builder.Services.AddOutputCache();
        builder.Services.AddSingleton(state);

        WebApplication app = builder.Build();
        app.UseRouting();
        app.UseOutputCache();
        app.MapHandlers(handlerRegistry =>
        {
            handlerRegistry.Register<GetProductSnapshotsCommand, TestGetProductSnapshotsCommandHandler, GetProductSnapshotsRpcResult>();
        });

        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        return new TestRemoteProductHost(app, state, app.Urls.Single());
    }

    public async ValueTask DisposeAsync()
    {
        await this.app.DisposeAsync().ConfigureAwait(false);
    }

    private static int GetFreeTcpPort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return port;
    }

    public sealed class ProductRemoteHealthEndpoint : EndpointWithoutRequest
    {
        public override void Configure()
        {
            Get("/_remote/product/health");
            AllowAnonymous();
        }

        public override Task HandleAsync(CancellationToken ct)
        {
            this.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
            return this.HttpContext.Response.WriteAsync("ok", ct);
        }
    }

    public sealed class TestRemoteProductState(Guid productId, string productName)
    {
        private int callCount;

        public Guid ProductId { get; } = productId;

        public string ProductName { get; } = productName;

        public int CallCount => this.callCount;

        public void IncrementCallCount()
        {
            Interlocked.Increment(ref this.callCount);
        }
    }

    public sealed class TestGetProductSnapshotsCommandHandler(TestRemoteProductState state)
        : ICommandHandler<GetProductSnapshotsCommand, GetProductSnapshotsRpcResult>
    {
        private readonly TestRemoteProductState state = state;

        public Task<GetProductSnapshotsRpcResult> ExecuteAsync(GetProductSnapshotsCommand command, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(command);
            ct.ThrowIfCancellationRequested();

            this.state.IncrementCallCount();

            GetProductSnapshotsRpcResult result = new();
            foreach (Guid requestedProductId in command.ProductIds.Distinct())
            {
                if (requestedProductId == this.state.ProductId)
                {
                    result.Items.Add(new ProductSnapshotRpcItem
                    {
                        ProductId = this.state.ProductId,
                        Name = this.state.ProductName,
                        Sku = "REMOTE-SKU",
                        Barcode = "9999999999999",
                        SnapshotVersion = "remote-v1",
                    });
                }
            }

            return Task.FromResult(result);
        }
    }
}