using System.Net;
using System.Net.Sockets;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Infrastructure;
using SharedKernel.Grpc.Contracts.Remote.V1.Labels;

namespace Device.IntegrationTests.TestSupport;

internal sealed class TestRemoteLabelHost : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly TestRemoteLabelState state;

    private TestRemoteLabelHost(WebApplication app, TestRemoteLabelState state, string baseAddress)
    {
        this.app = app;
        this.state = state;
        this.BaseAddress = baseAddress;
    }

    public string BaseAddress { get; }

    public int CallCount => this.state.CallCount;

    public static async Task<TestRemoteLabelHost> StartAsync(Guid jobId, string status, CancellationToken cancellationToken)
    {
        var state = new TestRemoteLabelState(jobId, status);

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
            handlerRegistry.Register<EnqueueRenderJobCommand, TestEnqueueRenderJobCommandHandler, EnqueueRenderJobRpcResult>();
        });

        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        return new TestRemoteLabelHost(app, state, app.Urls.Single());
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

    private sealed class LabelRemoteHealthEndpoint : EndpointWithoutRequest
    {
        public override void Configure()
        {
            Get("/_remote/label/health");
            AllowAnonymous();
        }

        public override Task HandleAsync(CancellationToken ct)
        {
            this.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
            return this.HttpContext.Response.WriteAsync("ok", ct);
        }
    }

    private sealed class TestRemoteLabelState(Guid jobId, string status)
    {
        private int callCount;

        public Guid JobId { get; } = jobId;

        public string Status { get; } = status;

        public int CallCount => this.callCount;

        public void IncrementCallCount()
        {
            Interlocked.Increment(ref this.callCount);
        }
    }

    private sealed class TestEnqueueRenderJobCommandHandler(TestRemoteLabelState state)
        : ICommandHandler<EnqueueRenderJobCommand, EnqueueRenderJobRpcResult>
    {
        private readonly TestRemoteLabelState state = state;

        public Task<EnqueueRenderJobRpcResult> ExecuteAsync(EnqueueRenderJobCommand command, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(command);
            ct.ThrowIfCancellationRequested();

            this.state.IncrementCallCount();

            return Task.FromResult(new EnqueueRenderJobRpcResult
            {
                JobId = this.state.JobId,
                Status = this.state.Status,
            });
        }
    }
}