using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Teck.Cloud.Commerce.Gateway.Http;

/// <summary>Minimal operational probes for edge processes that own no database or broker.</summary>
public static class CommerceEdgeHealth
{
    /// <summary>Maps liveness, startup and process readiness without dependency probes owned by backend services.</summary>
    /// <param name="app">The edge application.</param>
    /// <returns>The application.</returns>
    public static WebApplication MapCommerceEdgeHealth(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _ = app.MapGet("/health/live", () => Results.Text("live")).AllowAnonymous().ExcludeFromDescription();
        _ = app.MapGet("/health/startup", () => Results.Text("started")).AllowAnonymous().ExcludeFromDescription();
        _ = app.MapGet("/health/ready", () => Results.Text("ready")).AllowAnonymous().ExcludeFromDescription();
        return app;
    }
}
