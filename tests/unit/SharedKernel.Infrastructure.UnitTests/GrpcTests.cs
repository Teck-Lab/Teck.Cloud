using System.Net;
using System.Net.Http.Headers;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Persistence.Database;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.UnitTests;

/// <summary>Exercises gRPC credentials through the real client factory without an HTTP request context.</summary>
public sealed class GrpcTests
{
    /// <summary>Background callers propagate established tenant scope and asynchronous credentials.</summary>
    /// <returns>The verification task.</returns>
    /// <param name="address">The explicit transport address.</param>
    /// <param name="transport">The intended protection boundary.</param>
    [Theory]
    [InlineData("https://service.test", IdentityTransport.Https)]
    [InlineData("http://service.test", IdentityTransport.IsolatedDevelopment)]
    [InlineData("http://service.test", IdentityTransport.AmbientMesh)]
    public async Task BackgroundGrpcUsesScopedIdentity(string address, IdentityTransport transport)
    {
        using var handler = new CapturingHandler();
        var services = new ServiceCollection();
        _ = services.AddPersistenceFoundation();
        var tokens = Substitute.For<IAccessTokenProvider>();
        _ = tokens.GetTokenAsync("service", Arg.Any<CancellationToken>()).Returns("test-only-token");
        _ = services.AddScoped(_ => tokens);
        _ = services.AddGrpcClient<EchoClient>(options => options.Address = new Uri(address))
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddOperationCredentials("service", transport);
        var provider = services.BuildServiceProvider();
        await using var disposeProvider = provider.ConfigureAwait(true);
        await using var scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<OperationContext>().Initialize(DataScope.ForTenant("alpha"), "background-job");
        var result = await scope.ServiceProvider.GetRequiredService<EchoClient>().CallAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        result.ShouldBe("ok"u8.ToArray());
        handler.Authorization.ShouldBe("Bearer test-only-token");
        handler.Tenant.ShouldBeNull();
        _ = await tokens.Received(1).GetTokenAsync("service", Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    /// <summary>Unprotected HTTP cannot silently send a credential when the default HTTPS policy is selected.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task DefaultTransportRejectsHttpBeforeCredentialAcquisition()
    {
        using var handler = new CapturingHandler();
        var services = new ServiceCollection();
        _ = services.AddPersistenceFoundation();
        var tokens = Substitute.For<IAccessTokenProvider>();
        _ = services.AddScoped(_ => tokens);
        _ = services.AddGrpcClient<EchoClient>(options => options.Address = new Uri("http://service.test"))
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddOperationCredentials("service");
        var provider = services.BuildServiceProvider();
        await using var disposeProvider = provider.ConfigureAwait(true);
        await using var scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<OperationContext>().Initialize(DataScope.ForTenant("alpha"), "background-job");
        _ = await Should.ThrowAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<EchoClient>().CallAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
        handler.Authorization.ShouldBeNull();
        _ = await tokens.DidNotReceive().GetTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    /// <summary>Minimal typed gRPC client fixture.</summary>
    public sealed class EchoClient : ClientBase<EchoClient>
    {
        private static readonly Method<byte[], byte[]> Echo = new(MethodType.Unary, "test", "echo", Marshallers.Create<byte[]>(bytes => bytes, bytes => bytes), Marshallers.Create<byte[]>(bytes => bytes, bytes => bytes));

        /// <summary>Initializes a new instance of the <see cref="EchoClient"/> class.</summary>
        /// <param name="callInvoker">Factory-configured invoker.</param>
        public EchoClient(CallInvoker callInvoker)
            : base(callInvoker)
        {
        }

        private EchoClient(ClientBaseConfiguration configuration)
            : base(configuration)
        {
        }

        /// <summary>Calls the fixture method.</summary>
        /// <param name="cancellationToken">The call cancellation.</param>
        /// <returns>The response bytes.</returns>
        public async Task<byte[]> CallAsync(CancellationToken cancellationToken)
        {
            using var call = CallInvoker.AsyncUnaryCall(Echo, host: null, new CallOptions(cancellationToken: cancellationToken), []);
            return await call.ResponseAsync.ConfigureAwait(false);
        }

        /// <inheritdoc/>
        protected override EchoClient NewInstance(ClientBaseConfiguration configuration) => new(configuration);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }

        public string? Tenant { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            Tenant = request.Headers.TryGetValues("x-tenant-id", out var tenants) ? tenants.Single() : null;
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
