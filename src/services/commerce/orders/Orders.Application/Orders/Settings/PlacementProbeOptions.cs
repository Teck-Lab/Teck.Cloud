namespace Teck.Cloud.Commerce.Orders.Application.Orders.Settings;

/// <summary>Readiness probe scheduling bounds.</summary>
public sealed class PlacementProbeOptions
{
    /// <summary>Gets or sets the scan interval in seconds.</summary>
    public int ScanIntervalSeconds { get; set; } = 30;

    /// <summary>Gets or sets the maximum claims per scan.</summary>
    public int BatchSize { get; set; } = 25;

    /// <summary>Gets or sets the lease duration in seconds.</summary>
    public int LeaseSeconds { get; set; } = 60;

    /// <summary>Gets or sets the total probe bound per item in seconds.</summary>
    public int ProbeTimeoutSeconds { get; set; } = 10;

    /// <summary>Gets or sets the successful refresh interval in seconds.</summary>
    public int RefreshSeconds { get; set; } = 300;

    /// <summary>Gets or sets the transient retry interval in seconds.</summary>
    public int RetrySeconds { get; set; } = 300;
}
