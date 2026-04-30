using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Order.Api.Endpoints.V1.Orders.CreateFromBasket;
using SharedKernel.Infrastructure.Endpoints;

namespace Order.IntegrationTests.TestSupport;

internal sealed class TestOrderApiHost : IAsyncDisposable
{
    private readonly WebApplication app;

    private TestOrderApiHost(WebApplication app, HttpClient client)
    {
        this.app = app;
        this.Client = client;
    }

    public HttpClient Client { get; }

    public static async Task<TestOrderApiHost> StartAsync(ISender sender)
    {
        ArgumentNullException.ThrowIfNull(sender);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });

        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddOutputCache();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(TestAuthHandler.SchemeName, static _ => { });

        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, TestAuthorizationPolicyProvider>();

        builder.Services.AddSingleton(sender);
        builder.Services.AddFastEndpointsInfrastructure(typeof(CreateOrderFromBasketEndpoint).Assembly);

        WebApplication app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseFastEndpointsInfrastructure("order");

        await app.StartAsync(TestContext.Current.CancellationToken);
        return new TestOrderApiHost(app, app.GetTestClient());
    }

    public async ValueTask DisposeAsync()
    {
        this.Client.Dispose();
        await this.app.DisposeAsync();
    }
}
