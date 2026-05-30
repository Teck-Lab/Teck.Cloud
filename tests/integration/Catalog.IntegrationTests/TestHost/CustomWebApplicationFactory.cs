using Catalog.Api.Endpoints.V1.Brands;
using Catalog.IntegrationTests.TestSupport;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Infrastructure.Endpoints;

namespace Catalog.IntegrationTests.TestHost;

internal sealed class CustomWebApplicationFactory : IAsyncDisposable
{
    private readonly WebApplication app;

    private CustomWebApplicationFactory(WebApplication app, HttpClient client)
    {
        this.app = app;
        this.Client = client;
    }

    public HttpClient Client { get; }

    public static async Task<CustomWebApplicationFactory> StartAsync(ISender sender)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });

        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(TestAuthHandler.SchemeName, static _ => { });

        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, TestAuthorizationPolicyProvider>();
        builder.Services.AddSingleton(sender);
        builder.Services.AddSingleton<IMediator>(Substitute.For<IMediator>());
        builder.Services.AddFastEndpointsInfrastructure(typeof(CreateBrandEndpoint).Assembly);

        WebApplication app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseFastEndpointsInfrastructure("catalog");

        await app.StartAsync(TestContext.Current.CancellationToken);
        return new CustomWebApplicationFactory(app, app.GetTestClient());
    }

    public async ValueTask DisposeAsync()
    {
        this.Client.Dispose();
        await this.app.DisposeAsync();
    }
}
