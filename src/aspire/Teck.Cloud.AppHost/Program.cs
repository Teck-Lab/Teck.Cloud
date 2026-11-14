namespace Teck.Cloud.AppHost;

/// <summary>Local development dependencies and isolated deployment verification.</summary>
public static class Program
{
    /// <summary>Starts the selected local topology.</summary>
    /// <param name="args">Standard Aspire configuration arguments.</param>
    /// <returns>The application lifetime.</returns>
    /// <exception cref="InvalidOperationException">An unknown profile was selected.</exception>
    public static async Task Main(string[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);
        builder.AddStartupPreflight();
        switch (builder.Configuration["Profile"] ?? "development")
        {
            case "development":
                builder.AddFoundationServices();
                _ = builder.AddFoundationRedisSentinel();
                break;
            case "sentinel":
                _ = builder.AddFoundationRedisSentinel();
                break;
            case "kind":
                builder.AddDeploymentCluster();
                break;
            default:
                throw new InvalidOperationException("Profile must be development, sentinel or kind.");
        }

        var app = builder.Build();
        await using var disposeApp = app.ConfigureAwait(false);
        await app.RunAsync().ConfigureAwait(false);
    }
}
