namespace SharedKernel.Migration.Models;

/// <summary>
/// Result of a database migration operation.
/// </summary>
public sealed record MigrationResult
{
    /// <summary>
    /// Gets a value indicating whether the migration was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the number of scripts that were applied.
    /// </summary>
    public required int ScriptsApplied { get; init; }

    /// <summary>
    /// Gets the duration of the migration operation.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the error message if the migration failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the list of scripts that were applied.
    /// </summary>
    public required IReadOnlyList<string> AppliedScripts { get; init; }

    /// <summary>
    /// Gets the database provider used for the migration.
    /// </summary>
    public string? Provider { get; init; }

    /// <summary>
    /// Creates a successful migration result.
    /// </summary>
    public static MigrationResult Successful(
        int scriptsApplied,
        TimeSpan duration,
        IReadOnlyList<string> appliedScripts,
        string? provider = null) =>
        new()
        {
            Success = true,
            ScriptsApplied = scriptsApplied,
            Duration = duration,
            AppliedScripts = appliedScripts,
            Provider = provider,
        };

    /// <summary>
    /// Creates a failed migration result.
    /// </summary>
    public static MigrationResult Failed(
        string errorMessage,
        TimeSpan duration,
        string? provider = null) =>
        new()
        {
            Success = false,
            ScriptsApplied = 0,
            Duration = duration,
            ErrorMessage = errorMessage,
            AppliedScripts = Array.Empty<string>(),
            Provider = provider,
        };
}
