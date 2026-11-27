using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGenerations;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;

namespace Teck.Cloud.Commerce.Licensing.Infrastructure.Database;

/// <summary>Untracked, save-guarded licensing read model with the same tenant isolation.</summary>
/// <param name="options">Provider and model options.</param>
/// <param name="operation">The established tenant operation.</param>
/// <param name="clock">Optional injected clock.</param>
public sealed class LicensingReadDbContext(
    DbContextOptions<LicensingReadDbContext> options,
    OperationContext operation,
    TimeProvider? clock = null)
    : ScopedDbContext(options, operation, readOnly: true, clock)
{
    /// <summary>Gets license grants.</summary>
    public DbSet<LicenseGrant> Grants => Set<LicenseGrant>();

    /// <summary>Gets generation fences.</summary>
    public DbSet<LicenseGeneration> Generations => Set<LicenseGeneration>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        _ = modelBuilder.ApplyConfigurationsFromAssembly(typeof(LicensingReadDbContext).Assembly, ModelConfigurationFilters.ReadConfigurationsFilter);
        base.OnModelCreating(modelBuilder);
    }
}
