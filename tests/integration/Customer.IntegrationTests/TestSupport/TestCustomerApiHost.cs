using Customer.Api.Endpoints.V1.Tenants.GetCurrentTenantProfile;
using Finbuckle.MultiTenant.Abstractions;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.MultiTenant;

namespace Customer.IntegrationTests.TestSupport;

internal sealed class TestCustomerApiHost : IAsyncDisposable
{
    private readonly WebApplication app;

    private TestCustomerApiHost(WebApplication app, HttpClient client)
    {
        this.app = app;
        this.Client = client;
    }

    public HttpClient Client { get; }

    public static async Task<TestCustomerApiHost> StartAsync(
        ISender sender,
        IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor = null)
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
        builder.Services.AddFastEndpointsInfrastructure(typeof(GetCurrentTenantProfileEndpoint).Assembly);

        if (tenantContextAccessor is not null)
        {
            builder.Services.AddSingleton(tenantContextAccessor);
        }

        WebApplication app = builder.Build();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseFastEndpointsInfrastructure("customer");

        await app.StartAsync(TestContext.Current.CancellationToken);

        return new TestCustomerApiHost(app, app.GetTestClient());
    }

    public async ValueTask DisposeAsync()
    {
        this.Client.Dispose();
        await this.app.DisposeAsync();
    }
}
