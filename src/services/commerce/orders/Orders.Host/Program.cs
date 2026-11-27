namespace Teck.Cloud.Commerce.Orders.Host;

/// <summary>Process entrypoint; host composition lives in <see cref="OrdersHost"/>.</summary>
public static class Program
{
    /// <summary>Runs the service or applies migrations.</summary>
    /// <param name="args">Process arguments.</param>
    /// <returns>The process lifetime.</returns>
    public static Task Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return RunAsync(args);
    }

    private static async Task RunAsync(string[] args)
    {
        if (Array.Exists(args, argument => string.Equals(argument, "--migrate", StringComparison.Ordinal)))
        {
            await OrdersHost.MigrateAsync(args).ConfigureAwait(false);
            return;
        }

        var app = OrdersHost.Build(args);
        await using (app.ConfigureAwait(false))
        {
            await app.RunAsync().ConfigureAwait(false);
        }
    }
}
