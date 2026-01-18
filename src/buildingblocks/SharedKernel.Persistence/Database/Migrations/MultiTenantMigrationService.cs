#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Core.Pricing;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Persistence.Database.MultiTenant;
using SharedKernel.Secrets;

namespace SharedKernel.Persistence.Database.Migrations;

/// <summary>
/// Multi-tenant migration service that handles database migrations for shared and dedicated tenant databases.
/// </summary>
/// <typeparam name="TDbContext">The database context type.</typeparam>
public sealed class MultiTenantMigrationService<TDbContext> : IMigrationService
    where TDbContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITenantDbConnectionResolver _connectionResolver;
    private readonly IVaultSecretsManager _secretsManager;
    private readonly ILogger<MultiTenantMigrationService<TDbContext>> _logger;
    private readonly DatabaseProvider _defaultProvider;

    public MultiTenantMigrationService(
        IServiceProvider serviceProvider,
        ITenantDbConnectionResolver connectionResolver,
        IVaultSecretsManager secretsManager,
        ILogger<MultiTenantMigrationService<TDbContext>> logger,
        DatabaseProvider defaultProvider)
    {
        _serviceProvider = serviceProvider;
        _connectionResolver = connectionResolver;
        _secretsManager = secretsManager;
        _logger = logger;
        _defaultProvider = defaultProvider;
    }

    /// <inheritdoc />
    public async Task<MigrationResult> MigrateTenantDatabaseAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting migration for tenant {TenantId}", tenantId);

        try
        {
            // Get tenant connection info
            var tenantInfo = new TenantDetails { Id = tenantId };
            var (_, _, provider, strategy) = _connectionResolver.ResolveTenantConnection(tenantInfo);

            // Determine if this is a dedicated database
            if (strategy == DatabaseStrategy.Shared)
            {
                _logger.LogInformation(
                    "Tenant {TenantId} uses shared database, skipping dedicated migration",
                    tenantId);
                return MigrationResult.CreateSuccess(tenantId, 0, TimeSpan.Zero);
            }

            // Get admin credentials from Vault
            var credentials = await _secretsManager.GetDatabaseCredentialsAsync(
                tenantId,
                cancellationToken);

            // Build admin connection string
            var adminConnectionString = credentials.GetAdminConnectionString(provider.Name);

            // Create DbContext with admin credentials
            using var scope = _serviceProvider.CreateScope();
            var dbContext = CreateDbContextWithConnectionString(scope.ServiceProvider, adminConnectionString);

            // Run migrations
            var runner = new EFCoreMigrationRunner<TDbContext>(
                _serviceProvider.GetRequiredService<ILogger<EFCoreMigrationRunner<TDbContext>>>());

            return await runner.ApplyMigrationsAsync(dbContext, tenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate tenant database for {TenantId}", tenantId);
            return MigrationResult.CreateFailure(tenantId, ex.Message, TimeSpan.Zero);
        }
    }

    /// <inheritdoc />
    public async Task<MigrationResult> MigrateSharedDatabaseAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting migration for shared database");

        try
        {
            // Get shared database admin credentials from Vault
            var credentials = await _secretsManager.GetSharedDatabaseCredentialsAsync(cancellationToken);

            // Build admin connection string
            var adminConnectionString = credentials.GetAdminConnectionString(_defaultProvider.Name);

            // Create DbContext with admin credentials
            using var scope = _serviceProvider.CreateScope();
            var dbContext = CreateDbContextWithConnectionString(scope.ServiceProvider, adminConnectionString);

            // Run migrations
            var runner = new EFCoreMigrationRunner<TDbContext>(
                _serviceProvider.GetRequiredService<ILogger<EFCoreMigrationRunner<TDbContext>>>());

            return await runner.ApplyMigrationsAsync(dbContext, null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate shared database");
            return MigrationResult.CreateFailure(null, ex.Message, TimeSpan.Zero);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<MigrationResult>> MigrateAllDatabasesAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting migration for all databases");

        var results = new List<MigrationResult>();

        // 1. Migrate shared database first
        var sharedResult = await MigrateSharedDatabaseAsync(cancellationToken);
        results.Add(sharedResult);

        if (!sharedResult.Success)
        {
            _logger.LogError("Shared database migration failed, skipping tenant migrations");
            return results;
        }

        // 2. Get list of tenants with dedicated databases from shared database
        var tenantIds = await GetTenantsWithDedicatedDatabasesAsync(cancellationToken);

        _logger.LogInformation("Found {Count} tenants with dedicated databases", tenantIds.Count);

        // 3. Migrate each dedicated tenant database
        foreach (var tenantId in tenantIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var result = await MigrateTenantDatabaseAsync(tenantId, cancellationToken);
            results.Add(result);
        }

        var successCount = results.Count(r => r.Success);
        _logger.LogInformation(
            "Completed migration for all databases: {SuccessCount}/{TotalCount} successful",
            successCount,
            results.Count);

        return results;
    }

    /// <inheritdoc />
    public async Task<bool> HasPendingMigrationsAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantInfo = new TenantDetails { Id = tenantId };
            var (_, _, provider, strategy) = _connectionResolver.ResolveTenantConnection(tenantInfo);

            if (strategy == DatabaseStrategy.Shared)
            {
                // Check shared database
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
                var runner = new EFCoreMigrationRunner<TDbContext>(
                    _serviceProvider.GetRequiredService<ILogger<EFCoreMigrationRunner<TDbContext>>>());
                return await runner.HasPendingMigrationsAsync(dbContext, cancellationToken);
            }

            // Get credentials for dedicated database
            var credentials = await _secretsManager.GetDatabaseCredentialsAsync(tenantId, cancellationToken);
            var connectionString = credentials.GetApplicationConnectionString(provider.Name);

            using var dedicatedScope = _serviceProvider.CreateScope();
            var dedicatedContext = CreateDbContextWithConnectionString(dedicatedScope.ServiceProvider, connectionString);
            var dedicatedRunner = new EFCoreMigrationRunner<TDbContext>(
                _serviceProvider.GetRequiredService<ILogger<EFCoreMigrationRunner<TDbContext>>>());

            return await dedicatedRunner.HasPendingMigrationsAsync(dedicatedContext, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check pending migrations for tenant {TenantId}", tenantId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<MigrationStatus> GetMigrationStatusAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantInfo = new TenantDetails { Id = tenantId };
            var (_, _, provider, strategy) = _connectionResolver.ResolveTenantConnection(tenantInfo);

            TDbContext dbContext;
            IServiceScope scope;

            if (strategy == DatabaseStrategy.Shared)
            {
                scope = _serviceProvider.CreateScope();
                dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
            }
            else
            {
                var credentials = await _secretsManager.GetDatabaseCredentialsAsync(tenantId, cancellationToken);
                var connectionString = credentials.GetApplicationConnectionString(provider.Name);
                scope = _serviceProvider.CreateScope();
                dbContext = CreateDbContextWithConnectionString(scope.ServiceProvider, connectionString);
            }

            using (scope)
            {
                var runner = new EFCoreMigrationRunner<TDbContext>(
                    _serviceProvider.GetRequiredService<ILogger<EFCoreMigrationRunner<TDbContext>>>());

                return await runner.GetMigrationStatusAsync(dbContext, tenantId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get migration status for tenant {TenantId}", tenantId);
            throw;
        }
    }

    private TDbContext CreateDbContextWithConnectionString(IServiceProvider serviceProvider, string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();

        // Configure the database provider based on the connection string or provider
        if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) && connectionString.Contains("Port=", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseNpgsql(connectionString);
        }
        else if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
                 (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) && !connectionString.Contains("Port=", StringComparison.OrdinalIgnoreCase)))
        {
            optionsBuilder.UseSqlServer(connectionString);
        }
        else if (connectionString.Contains("Uid=", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseMySQL(connectionString);
        }
        else
        {
            // Default to PostgreSQL
            optionsBuilder.UseNpgsql(connectionString);
        }

        return (TDbContext)Activator.CreateInstance(typeof(TDbContext), optionsBuilder.Options)!;
    }

    private async Task<IReadOnlyList<string>> GetTenantsWithDedicatedDatabasesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            // Try to get the CustomerApiTenantStore which has the filtered query capability
            var tenantStore = _serviceProvider.GetService<CustomerApiTenantStore>();

            if (tenantStore != null)
            {
                // Use the optimized query that filters at the API level
                // Get Dedicated tenants
                var dedicatedResult = await tenantStore.GetPaginatedTennantsAsync(
                    DatabaseStrategy.Dedicated,
                    size: 1000,
                    page: 0);

                // Get External tenants
                var externalResult = await tenantStore.GetPaginatedTennantsAsync(
                    DatabaseStrategy.External,
                    size: 1000,
                    page: 0);

                var tenantIds = dedicatedResult.Items
                    .Concat(externalResult.Items)
                    .Where(t => !string.IsNullOrEmpty(t.Id))
                    .Select(t => t.Id!)
                    .ToList();

                _logger.LogInformation(
                    "Found {Count} tenants with dedicated/external databases (Dedicated: {DedicatedCount}, External: {ExternalCount})",
                    tenantIds.Count,
                    dedicatedResult.Items.Count,
                    externalResult.Items.Count);

                return tenantIds;
            }

            // Fallback: Try using the typed tenant store (Finbuckle's generic interface)
            var typedTenantStore = _serviceProvider.GetService<Finbuckle.MultiTenant.Abstractions.IMultiTenantStore<TenantDetails>>();

            if (typedTenantStore != null)
            {
                var allTenants = await typedTenantStore.GetAllAsync();

                if (allTenants == null || !allTenants.Any())
                {
                    _logger.LogWarning("No tenants returned from typed store - returning empty list");
                    return Array.Empty<string>();
                }

                var filteredTenants = allTenants
                    .Where(t => t.DatabaseStrategy == DatabaseStrategy.Dedicated.Name ||
                               t.DatabaseStrategy == DatabaseStrategy.External.Name)
                    .Where(t => !string.IsNullOrEmpty(t.Id))
                    .Select(t => t.Id!)
                    .ToList();

                _logger.LogInformation(
                    "Found {Count} tenants with dedicated/external databases (via typed store)",
                    filteredTenants.Count);

                return filteredTenants;
            }

            // Final fallback: Use base interface (returns TenantInfo)
            var baseTenantStore = _serviceProvider.GetService<SharedKernel.Infrastructure.MultiTenant.IMultiTenantStore>();

            if (baseTenantStore == null)
            {
                _logger.LogWarning("No tenant store registered - returning empty list");
                return Array.Empty<string>();
            }

            // Get all tenants - these are TenantInfo, not TenantDetails
            var allBaseTenants = await baseTenantStore.GetAllAsync();

            if (allBaseTenants == null || allBaseTenants.Length == 0)
            {
                _logger.LogWarning("No tenants returned from store - returning empty list");
                return Array.Empty<string>();
            }

            // Since TenantInfo doesn't have DatabaseStrategy, return all tenant IDs
            var tenantIdsFromBase = allBaseTenants
                .Where(t => !string.IsNullOrEmpty(t.Id))
                .Select(t => t.Id!)
                .ToList();

            _logger.LogInformation(
                "Found {Count} tenants with dedicated/external databases (via fallback query)",
                tenantIdsFromBase.Count);

            return tenantIdsFromBase;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tenants with dedicated databases");
            return Array.Empty<string>();
        }
    }
}
