#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Core.Pricing;

namespace SharedKernel.Persistence.Database.Migrations;

/// <summary>
/// Extension methods for registering database migration services.
/// </summary>
public static class MigrationServiceExtensions
{
    /// <summary>
    /// Adds multi-tenant database migration services.
    /// </summary>
    /// <typeparam name="TDbContext">The database context type.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="defaultProvider">Default database provider.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddMultiTenantMigrations<TDbContext>(
        this IServiceCollection services,
        DatabaseProvider defaultProvider)
        where TDbContext : DbContext
    {
        services.AddScoped<IMigrationService>(sp =>
            new MultiTenantMigrationService<TDbContext>(
                sp,
                sp.GetRequiredService<MultiTenant.ITenantDbConnectionResolver>(),
                sp.GetRequiredService<Secrets.IVaultSecretsManager>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MultiTenantMigrationService<TDbContext>>>(),
                defaultProvider));

        services.AddSingleton(sp =>
            new EFCoreMigrationRunner<TDbContext>(
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EFCoreMigrationRunner<TDbContext>>>()));

        return services;
    }

    /// <summary>
    /// Migrates all databases (shared + dedicated tenants) on application startup.
    /// </summary>
    /// <param name="serviceProvider">Service provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Migration results.</returns>
    public static async Task<IReadOnlyCollection<MigrationResult>> MigrateAllDatabasesOnStartupAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
        return await migrationService.MigrateAllDatabasesAsync(cancellationToken);
    }

    /// <summary>
    /// Migrates the shared database only on application startup.
    /// </summary>
    /// <param name="serviceProvider">Service provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Migration result.</returns>
    public static async Task<MigrationResult> MigrateSharedDatabaseOnStartupAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
        return await migrationService.MigrateSharedDatabaseAsync(cancellationToken);
    }
}
