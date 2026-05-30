using Basket.Api.Endpoints.V1.Basket.AddItem;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Infrastructure.Endpoints;

namespace Basket.IntegrationTests.TestSupport;

internal sealed class TestBasketApiHost : IAsyncDisposable
{
    private readonly WebApplication app;

    private TestBasketApiHost(WebApplication app, HttpClient client)
    {
        this.app = app;
        this.Client = client;
    }

    public HttpClient Client { get; }

    public static async Task<TestBasketApiHost> StartAsync(ISender sender)
    {
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
        builder.Services.AddFastEndpointsInfrastructure(typeof(AddBasketItemEndpoint).Assembly);

        WebApplication app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseFastEndpointsInfrastructure("basket");

        await app.StartAsync(TestContext.Current.CancellationToken);
        return new TestBasketApiHost(app, app.GetTestClient());
    }

    public async ValueTask DisposeAsync()
    {
        this.Client.Dispose();
        await this.app.DisposeAsync();
    }
}
