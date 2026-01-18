using Ardalis.SmartEnum;

namespace SharedKernel.Core.Pricing;

/// <summary>
/// Represents the database provider type using SmartEnum.
/// </summary>
public sealed class DatabaseProvider : SmartEnum<DatabaseProvider>
{
    /// <summary>
    /// None/Default provider.
    /// </summary>
    public static readonly DatabaseProvider None = new(nameof(None), 0, "Unknown", "", 1.0m);

    /// <summary>
    /// PostgreSQL database provider (default, baseline cost).
    /// </summary>
    public static readonly DatabaseProvider PostgreSQL = new(nameof(PostgreSQL), 1, "PostgreSQL", "Npgsql", 1.0m);

    /// <summary>
    /// SQL Server database provider (premium cost due to licensing).
    /// </summary>
    public static readonly DatabaseProvider SqlServer = new(nameof(SqlServer), 2, "SQL Server", "Microsoft.Data.SqlClient", 1.5m);

    /// <summary>
    /// MySQL database provider (moderate cost).
    /// </summary>
    public static readonly DatabaseProvider MySQL = new(nameof(MySQL), 3, "MySQL", "MySql.Data.MySqlClient", 1.2m);

    /// <summary>
    /// Oracle database provider (enterprise cost).
    /// </summary>
    public static readonly DatabaseProvider Oracle = new(nameof(Oracle), 4, "Oracle", "Oracle.ManagedDataAccess.Client", 2.0m);

    /// <summary>
    /// MongoDB database provider (NoSQL moderate cost).
    /// </summary>
    public static readonly DatabaseProvider MongoDB = new(nameof(MongoDB), 5, "MongoDB", "MongoDB.Driver", 1.3m);

    /// <summary>
    /// Gets the display name of the provider.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the connection string provider name.
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    /// Gets the cost multiplier for this database provider.
    /// PostgreSQL is the baseline (1.0x), MySQL (1.2x), MongoDB (1.3x), SQL Server (1.5x), Oracle (2.0x).
    /// </summary>
    public decimal CostMultiplier { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseProvider"/> class.
    /// </summary>
    /// <param name="name">The name of the provider.</param>
    /// <param name="value">The value of the provider.</param>
    /// <param name="displayName">The display name of the provider.</param>
    /// <param name="providerName">The provider name for connection strings.</param>
    /// <param name="costMultiplier">The cost multiplier for this provider.</param>
    private DatabaseProvider(string name, int value, string displayName, string providerName, decimal costMultiplier)
        : base(name, value)
    {
        DisplayName = displayName;
        ProviderName = providerName;
        CostMultiplier = costMultiplier;
    }

    /// <summary>
    /// Gets a value indicating whether this provider supports advanced features.
    /// </summary>
    public bool SupportsAdvancedFeatures => this != None;

    /// <summary>
    /// Gets a value indicating whether this provider supports read replicas.
    /// </summary>
    public bool SupportsReadReplicas => this != None && this != MongoDB;

    /// <summary>
    /// Gets a value indicating whether this provider supports encryption at rest natively.
    /// </summary>
    public bool SupportsNativeEncryption => this == SqlServer || this == Oracle || this == PostgreSQL;

    /// <summary>
    /// Gets a value indicating whether this provider supports auto scaling.
    /// </summary>
    public bool SupportsAutoScaling => this != None;

    /// <summary>
    /// Gets a value indicating whether this provider supports JSON/NoSQL features.
    /// </summary>
    public bool SupportsJsonFeatures => this == PostgreSQL || this == MongoDB || this == SqlServer;

    /// <summary>
    /// Gets a value indicating whether this provider supports geographic distribution.
    /// </summary>
    public bool SupportsGeographicDistribution => this != None;

    /// <summary>
    /// Gets a value indicating whether this provider has built-in compliance features.
    /// </summary>
    public bool HasBuiltInCompliance => this == SqlServer || this == Oracle;

    /// <summary>
    /// Gets the default port for this database provider.
    /// </summary>
    public int DefaultPort => this switch
    {
        _ when this == PostgreSQL => 5432,
        _ when this == SqlServer => 1433,
        _ when this == MySQL => 3306,
        _ when this == Oracle => 1521,
        _ when this == MongoDB => 27017,
        _ => 0
    };

    /// <summary>
    /// Gets the file extension for migration files for this provider.
    /// </summary>
    public string MigrationFileExtension => this switch
    {
        _ when this == PostgreSQL => ".pgsql",
        _ when this == SqlServer => ".sql",
        _ when this == MySQL => ".mysql",
        _ when this == Oracle => ".oracle",
        _ when this == MongoDB => ".js",
        _ => ".sql"
    };

    /// <summary>
    /// Gets the maximum database size supported by this provider (in GB).
    /// </summary>
    public long MaxDatabaseSizeGB => this switch
    {
        _ when this == PostgreSQL => 32_000,
        _ when this == SqlServer => 524_272,
        _ when this == MySQL => 256_000,
        _ when this == Oracle => 8_000_000,
        _ when this == MongoDB => 16_000_000,
        _ => 1_000
    };

    /// <summary>
    /// Gets the licensing model for this provider.
    /// </summary>
    public string LicensingModel => this switch
    {
        _ when this == PostgreSQL => "Open Source",
        _ when this == SqlServer => "Commercial",
        _ when this == MySQL => "Dual License",
        _ when this == Oracle => "Commercial",
        _ when this == MongoDB => "Server Side Public License",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets the typical backup and restore speed (GB/hour).
    /// </summary>
    public decimal BackupRestoreSpeedGBPerHour => this switch
    {
        _ when this == PostgreSQL => 100m,
        _ when this == SqlServer => 150m,
        _ when this == MySQL => 80m,
        _ when this == Oracle => 200m,
        _ when this == MongoDB => 120m,
        _ => 50m
    };

    /// <summary>
    /// Validates if the given database options are compatible with this provider.
    /// </summary>
    /// <param name="options">The database options to validate.</param>
    /// <returns>True if compatible, otherwise false.</returns>
    public bool IsCompatibleWith(DatabaseOptions options)
    {
        if (options.HasReadReplicas && !SupportsReadReplicas) return false;
        if (options.RequiresAutoScaling && !SupportsAutoScaling) return false;
        if (options.RequiresGeographicDistribution && !SupportsGeographicDistribution) return false;

        return true;
    }
}
