using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;
using SharedKernel.Infrastructure.Hosting;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Exercises the actual gRPC client instrumentation with a controlled HTTP/2 response.</summary>
public sealed partial class TelemetryTests
{
    /// <summary>Client RPC spans keep the caller trace and report the RPC method.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task GrpcClientPreservesCallerTrace()
    {
        using var capture = new ActivityCapture();
        var builder = Host.CreateApplicationBuilder();
        builder.AddFoundationHost(exportTelemetry: false);
        _ = builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddProcessor(new SimpleActivityExportProcessor(capture)));
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using var handler = new GrpcResponseHandler();
        using var channel = GrpcChannel.ForAddress("https://service.test", new GrpcChannelOptions { HttpHandler = handler });
        var method = new Method<byte[], byte[]>(MethodType.Unary, "test", "echo", Marshallers.Create<byte[]>(bytes => bytes, bytes => bytes), Marshallers.Create<byte[]>(bytes => bytes, bytes => bytes));
        using var source = new ActivitySource("Teck.Cloud");
        using var parent = source.StartActivity("grpc-parent");
        _ = parent.ShouldNotBeNull();
        using var call = channel.CreateCallInvoker().AsyncUnaryCall(method, host: null, new CallOptions(cancellationToken: TestContext.Current.CancellationToken), []);
        (await call.ResponseAsync.ConfigureAwait(true)).ShouldBe("ok"u8.ToArray());
        host.Services.GetRequiredService<TracerProvider>().ForceFlush().ShouldBeTrue();
        var rpc = capture.Snapshot().Where(activity => activity.Kind == ActivityKind.Client && activity.Source.Name.Contains("Grpc", StringComparison.Ordinal)).ShouldHaveSingleItem();
        rpc.TraceId.ShouldBe(parent.TraceId);
        rpc.ParentSpanId.ShouldBe(parent.SpanId);
        rpc.GetTagItem("rpc.method").ShouldBe("test/echo");
        await host.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private sealed class GrpcResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Version = new Version(2, 0),
                Content = new ByteArrayContent([0, 0, 0, 0, 2, 111, 107]),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
            response.TrailingHeaders.Add("grpc-status", "0");
            return Task.FromResult(response);
        }
    }
}
