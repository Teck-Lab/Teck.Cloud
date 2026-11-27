using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGenerations;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Teck.Cloud.Commerce.Licensing.Infrastructure.Database;

/// <summary>Tracked licensing write model and the Wolverine transactional outbox owner.</summary>
/// <param name="options">Provider and model options.</param>
/// <param name="operation">The established tenant operation.</param>
/// <param name="outbox">The scoped official outbox.</param>
/// <param name="messaging">The active Wolverine message context.</param>
/// <param name="clock">Optional injected clock.</param>
public sealed class LicensingWriteDbContext(
    DbContextOptions<LicensingWriteDbContext> options,
    OperationContext operation,
    IDbContextOutbox outbox,
    IMessageContext messaging,
    TimeProvider? clock = null)
    : WriteDbContext(options, operation, outbox, messaging, clock)
{
    /// <summary>Gets tracked license grants.</summary>
    public DbSet<LicenseGrant> Grants => Set<LicenseGrant>();

    /// <summary>Gets tracked generation fences.</summary>
    public DbSet<LicenseGeneration> Generations => Set<LicenseGeneration>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        _ = modelBuilder.ApplyConfigurationsFromAssembly(typeof(LicensingWriteDbContext).Assembly, ModelConfigurationFilters.WriteConfigurationsFilter);
        base.OnModelCreating(modelBuilder);
    }
}
