namespace Teck.Cloud.Commerce.Orders.Application.Orders.Settings;

/// <summary>Provider work scheduling bounds.</summary>
public sealed class ProviderWorkOptions
{
    /// <summary>Gets or sets the scan interval in seconds.</summary>
    public int ScanIntervalSeconds { get; set; } = 2;

    /// <summary>Gets or sets the maximum claims per scan.</summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>Gets or sets the lease duration in seconds.</summary>
    public int LeaseSeconds { get; set; } = 60;

    /// <summary>Gets or sets the retry interval in seconds.</summary>
    public int RetrySeconds { get; set; } = 300;

    /// <summary>Gets or sets the number of hours after which an ambiguous create stops automatically.</summary>
    public int AmbiguityHours { get; set; } = 24;
}
