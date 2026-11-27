using SharedKernel.Infrastructure.Hosting;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Composition;
using Teck.Cloud.Commerce.Gateway.Http;

namespace Teck.Cloud.Commerce.CustomerGateway.Host;

/// <summary>Composes the standalone customer commerce edge process.</summary>
public static class CustomerGatewayHost
{
    /// <summary>Builds the customer edge with standard configuration and no fixture identity path.</summary>
    /// <param name="args">Host arguments.</param>
    /// <returns>The unstarted application.</returns>
    public static WebApplication Build(string[] args) => Build(args, configure: null);

    /// <summary>Builds the customer edge with an optional composition test hook.</summary>
    /// <param name="args">Host arguments.</param>
    /// <param name="configure">Optional late service replacement for isolated tests; never registers fixtures here.</param>
    /// <returns>The unstarted application.</returns>
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddFoundationHost();
        var authentication = CommerceAuth.FromConfiguration(builder.Configuration);
        var directory = CommerceDirectoryOptions.Load(builder.Configuration);
        var gateway = CommerceGatewayOptions.Load(builder.Configuration, requireLicensing: true);
        _ = builder.Services.AddCommerceGatewayDirectory(directory, authentication);
        _ = builder.Services.AddCommerceCustomerGatewayAuth(authentication);
        _ = builder.Services.AddCustomerCommerceGateway(gateway);
        _ = builder.Services.AddFoundationApi(global::CustomerGateway.Host.DiscoveredTypes.All);
        configure?.Invoke(builder);

        var app = builder.Build();
        _ = app.UseRouting();
        _ = app.UseCommerceProblemStatusPages();
        _ = app.UseAuthentication();
        _ = app.UseAuthorization();
        app.UseFoundationApi(exposeDocumentation: builder.Configuration.GetValue("Commerce:ExposeDocumentation", defaultValue: false));
        _ = app.MapCommerceEdgeHealth();
        return app;
    }
}
