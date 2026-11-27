using SharedKernel.Infrastructure.Hosting;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Composition;
using Teck.Cloud.Commerce.Gateway.Http;

namespace Teck.Cloud.Commerce.AdminGateway.Host;

/// <summary>Composes the standalone staff commerce edge process.</summary>
public static class AdminGatewayHost
{
    /// <summary>Builds the staff edge with standard configuration and no fixture identity path.</summary>
    /// <param name="args">Host arguments.</param>
    /// <returns>The unstarted application.</returns>
    public static WebApplication Build(string[] args) => Build(args, configure: null);

    /// <summary>Builds the staff edge with an optional composition test hook.</summary>
    /// <param name="args">Host arguments.</param>
    /// <param name="configure">Optional late service replacement for isolated tests; never registers fixtures here.</param>
    /// <returns>The unstarted application.</returns>
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddFoundationHost();
        var authentication = CommerceAuth.FromConfiguration(builder.Configuration);
        var directory = CommerceDirectoryOptions.Load(builder.Configuration);
        var gateway = CommerceGatewayOptions.Load(builder.Configuration, requireLicensing: false);
        _ = builder.Services.AddCommerceGatewayDirectory(directory, authentication);
        _ = builder.Services.AddCommerceAdminGatewayAuth(authentication);
        _ = builder.Services.AddAdminCommerceGateway(gateway);
        _ = builder.Services.AddFoundationApi(global::AdminGateway.Host.DiscoveredTypes.All);
        configure?.Invoke(builder);

        var app = builder.Build();
        _ = app.UseRouting();
        _ = app.UseCommerceProblemStatusPages();
        _ = app.UseAuthentication();
        _ = app.UseAuthorization();
        _ = app.Use(ValidateAdminTenantPathAsync);
        app.UseFoundationApi(exposeDocumentation: builder.Configuration.GetValue("Commerce:ExposeDocumentation", defaultValue: false));
        _ = app.MapCommerceEdgeHealth();
        return app;
    }

    private static async Task ValidateAdminTenantPathAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        if (context.Request.Path.StartsWithSegments("/v1/admin/commerce/tenants", StringComparison.Ordinal)
            && context.Request.RouteValues.TryGetValue("tenantId", out var value)
            && !CommerceRequestValidation.TryUuid(value as string, out _))
        {
            await CommerceHttpResults.InvalidRequest(context).ExecuteAsync(context).ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }
}
