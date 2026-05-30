using Billing.Api.Endpoints.V1.Billing;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SharedKernel.Infrastructure.Endpoints;

namespace Billing.IntegrationTests.TestSupport;

internal sealed class TestBillingApiHost : IAsyncDisposable
{
    private readonly WebApplication app;

    private TestBillingApiHost(WebApplication app, HttpClient client)
    {
        this.app = app;
        this.Client = client;
    }

    public HttpClient Client { get; }

    public static async Task<TestBillingApiHost> StartAsync(ISender sender)
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

        builder.Services.AddSingleton(sender);
        builder.Services.AddFastEndpointsInfrastructure(typeof(GetPaginatedBillingTransactionsEndpoint).Assembly);
        builder.Services.RemoveAll<IAuthorizationPolicyProvider>();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, TestAuthorizationPolicyProvider>();

        WebApplication app = builder.Build();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseFastEndpointsInfrastructure("billing");

        await app.StartAsync(TestContext.Current.CancellationToken);

        return new TestBillingApiHost(app, app.GetTestClient());
    }

    public async ValueTask DisposeAsync()
    {
        this.Client.Dispose();
        await this.app.DisposeAsync();
    }
}
