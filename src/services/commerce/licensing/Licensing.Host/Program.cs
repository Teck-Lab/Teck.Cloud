namespace Teck.Cloud.Commerce.Licensing.Host;

/// <summary>Separate licensing process entry point.</summary>
public static class Program
{
    /// <summary>Builds and runs the licensing host.</summary>
    /// <param name="args">Host configuration arguments.</param>
    /// <returns>The process lifetime.</returns>
    public static Task Main(string[] args) => LicensingHost.Build(args).RunAsync();
}
