namespace SharedKernel.Migration.Models;

/// <summary>
/// Options for database migrations.
/// </summary>
public sealed class MigrationOptions
{
    /// <summary>
    /// Gets or sets the path to the migration scripts directory.
    /// </summary>
    public string ScriptsPath { get; set; } = "Scripts";

    /// <summary>
    /// Gets or sets the database provider.
    /// </summary>
    public string Provider { get; set; } = "PostgreSQL";

    /// <summary>
    /// Gets or sets the schema name for the migration journal table.
    /// </summary>
    public string? JournalSchema { get; set; }

    /// <summary>
    /// Gets or sets the table name for the migration journal.
    /// </summary>
    public string JournalTable { get; set; } = "SchemaVersions";

    /// <summary>
    /// Gets or sets a value indicating whether to use transactions.
    /// </summary>
    public bool UseTransactions { get; set; } = true;

    /// <summary>
    /// Gets or sets the command timeout in seconds.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Gets or sets a value indicating whether to log script output.
    /// </summary>
    public bool LogScriptOutput { get; set; } = true;
}
