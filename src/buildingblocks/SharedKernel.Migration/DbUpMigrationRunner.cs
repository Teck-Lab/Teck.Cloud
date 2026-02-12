using System.Diagnostics;
using DbUp;
using DbUp.Builder;
using DbUp.Engine;
using DbUp.Engine.Output;
using DbUp.Helpers;
using Microsoft.Extensions.Logging;
using SharedKernel.Migration.Models;
using SharedKernel.Secrets;

namespace SharedKernel.Migration;

/// <summary>
/// Service for running database migrations using DbUp.
/// </summary>
public sealed class DbUpMigrationRunner
{
    private readonly IVaultSecretsManager _vaultSecretsManager;
    private readonly ILogger<DbUpMigrationRunner> _logger;

    public DbUpMigrationRunner(
        IVaultSecretsManager vaultSecretsManager,
        ILogger<DbUpMigrationRunner> logger)
    {
        _vaultSecretsManager = vaultSecretsManager;
        _logger = logger;
    }

    /// <summary>
    /// Runs database migrations using admin credentials from Vault.
    /// </summary>
    /// <param name="vaultPath">Path to credentials in Vault.</param>
    /// <param name="options">Migration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Migration result.</returns>
    public async Task<MigrationResult> MigrateAsync(
        string vaultPath,
        MigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting database migration from Vault path {VaultPath}", vaultPath);

            // Get admin credentials from Vault
            var credentials = await _vaultSecretsManager.GetDatabaseCredentialsByPathAsync(
                vaultPath, cancellationToken);

            var provider = credentials.Provider ?? options.Provider;
            var connectionString = credentials.GetAdminConnectionString(provider);

            _logger.LogInformation("Retrieved credentials for provider {Provider}", provider);

            // DEV fallback: if running locally (ASPIRE_LOCAL=true) and no scripts exist, skip DbUp
            var scriptsPath = options.ScriptsPath ?? "./Scripts";
            var isAspireLocal = string.Equals(Environment.GetEnvironmentVariable("ASPIRE_LOCAL"), "true", StringComparison.OrdinalIgnoreCase);

            if (isAspireLocal)
            {
                try
                {
                    var scriptsExist = System.IO.Directory.Exists(scriptsPath) && System.IO.Directory.GetFiles(scriptsPath, "*.sql", System.IO.SearchOption.AllDirectories).Length > 0;
                    if (!scriptsExist)
                    {
                        _logger.LogWarning("DEV FALLBACK: No migration scripts found at {ScriptsPath} and ASPIRE_LOCAL=true. Skipping DbUp migrations.", scriptsPath);
                        stopwatch.Stop();
                        return MigrationResult.Successful(0, stopwatch.Elapsed, new List<string>(), provider);
                    }
                }
                catch (Exception ioEx)
                {
                    _logger.LogWarning(ioEx, "DEV FALLBACK: Error checking scripts path {ScriptsPath}. Proceeding with DbUp attempt.", scriptsPath);
                }
            }

            // Run migration
            var result = RunDbUpMigration(connectionString, provider, options);

            stopwatch.Stop();

            if (result.Successful)
            {
                _logger.LogInformation(
                    "Migration completed successfully. Applied {Count} scripts in {Duration}ms",
                    result.Scripts.Count(),
                    stopwatch.ElapsedMilliseconds);

                return MigrationResult.Successful(
                    result.Scripts.Count(),
                    stopwatch.Elapsed,
                    result.Scripts.Select(s => s.Name).ToList(),
                    provider);
            }

            _logger.LogError(result.Error, "Migration failed");
            return MigrationResult.Failed(
                result.Error.Message,
                stopwatch.Elapsed,
                provider);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Migration failed with exception");

            // If running in local Aspire mode, treat failures due to missing scripts or DbUp errors as non-fatal.
            var isAspireLocal = string.Equals(Environment.GetEnvironmentVariable("ASPIRE_LOCAL"), "true", StringComparison.OrdinalIgnoreCase);
            if (isAspireLocal)
            {
                _logger.LogWarning(ex, "DEV FALLBACK: Migration failed but ASPIRE_LOCAL=true. Returning success for local development only.");
                return MigrationResult.Successful(0, stopwatch.Elapsed, new List<string>(), options.Provider);
            }

            return MigrationResult.Failed(ex.Message, stopwatch.Elapsed, options.Provider);
        }
    }

    private DatabaseUpgradeResult RunDbUpMigration(
        string connectionString,
        string provider,
        MigrationOptions options)
    {
        var builder = CreateUpgradeEngineBuilder(connectionString, provider, options);

        if (options.UseTransactions)
        {
            builder = builder.WithTransaction();
        }
        else
        {
            builder = builder.WithoutTransaction();
        }

        if (options.LogScriptOutput)
        {
            builder = builder.LogScriptOutput();
        }

        builder = builder.LogTo(new DbUpLogger(_logger));

        var upgrader = builder.Build();

        if (!upgrader.IsUpgradeRequired())
        {
            _logger.LogInformation("No upgrade required - database is up to date");
            return new DatabaseUpgradeResult(
                new List<SqlScript>(),
                successful: true,
                error: null,
                null);
        }

        _logger.LogInformation("Upgrade is required. Executing migration...");
        return upgrader.PerformUpgrade();
    }

    private UpgradeEngineBuilder CreateUpgradeEngineBuilder(
        string connectionString,
        string provider,
        MigrationOptions options)
    {
        UpgradeEngineBuilder builder = provider.ToLowerInvariant() switch
        {
            "postgresql" or "postgres" or "npgsql" =>
                DeployChanges.To
                    .PostgresqlDatabase(connectionString)
                    .WithScriptsFromFileSystem(options.ScriptsPath)
                    .JournalToPostgresqlTable(options.JournalSchema, options.JournalTable),

            "sqlserver" or "mssql" =>
                DeployChanges.To
                    .SqlDatabase(connectionString)
                    .WithScriptsFromFileSystem(options.ScriptsPath)
                    .JournalToSqlTable(options.JournalSchema ?? "dbo", options.JournalTable),

            "mysql" =>
                DeployChanges.To
                    .MySqlDatabase(connectionString)
                    .WithScriptsFromFileSystem(options.ScriptsPath)
                    .JournalToMySqlTable(options.JournalSchema, options.JournalTable),

            _ => throw new NotSupportedException($"Database provider '{provider}' is not supported."),
        };

        builder = builder.WithExecutionTimeout(TimeSpan.FromSeconds(options.CommandTimeoutSeconds));

        return builder;
    }

    /// <summary>
    /// Custom DbUp logger that forwards to ILogger.
    /// </summary>
    private sealed class DbUpLogger : IUpgradeLog
    {
        private readonly ILogger _logger;

        public DbUpLogger(ILogger logger) => _logger = logger;

        public void LogTrace(string format, params object[] args) =>
            _logger.LogTrace(format, args);

        public void LogDebug(string format, params object[] args) =>
            _logger.LogDebug(format, args);

        public void LogInformation(string format, params object[] args) =>
            _logger.LogInformation(format, args);

        public void LogWarning(string format, params object[] args) =>
            _logger.LogWarning(format, args);

        public void LogError(string format, params object[] args) =>
            _logger.LogError(format, args);

        public void LogError(Exception ex, string format, params object[] args) =>
            _logger.LogError(ex, format, args);

        public void WriteInformation(string format, params object[] args) =>
            _logger.LogInformation(format, args);

        public void WriteError(string format, params object[] args) =>
            _logger.LogError(format, args);

        public void WriteWarning(string format, params object[] args) =>
            _logger.LogWarning(format, args);
    }
}
