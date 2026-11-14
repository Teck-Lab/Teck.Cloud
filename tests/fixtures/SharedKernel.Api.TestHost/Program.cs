using SharedKernel.Infrastructure.Hosting;

namespace SharedKernel.Api.TestHost;

/// <summary>WebApplicationFactory entry point for foundation verification, not a deployed service.</summary>
public static class Program
{
    /// <summary>Starts the isolated test host.</summary>
    /// <param name="args">Host arguments.</param>
    /// <returns>The host lifetime.</returns>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddFoundationHost();
        _ = builder.Services.AddAuthorization();
        _ = builder.Services.AddFoundationApi(DiscoveredTypes.All);
        var app = builder.Build();
        await using var disposeApp = app.ConfigureAwait(false);
        _ = app.UseRouting();
        _ = app.UseAuthorization();
        app.UseFoundationApi(exposeDocumentation: true);
        app.MapFoundationHealth();
        await app.RunAsync().ConfigureAwait(false);
    }
}
