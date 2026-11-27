using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Baskets;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Idempotency;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.OrderAudit;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.PlacementProbe;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.ProviderEvent;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Reconciliation;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Database;

/// <summary>The tracked write model and Wolverine outbox context.</summary>
/// <param name="options">Provider options.</param>
/// <param name="operation">The scoped operation identity.</param>
/// <param name="outbox">Wolverine's scoped official outbox.</param>
/// <param name="messaging">The current Wolverine message context.</param>
/// <param name="clock">Optional application clock.</param>
public sealed class OrdersWriteDbContext(
    DbContextOptions<OrdersWriteDbContext> options,
    OperationContext operation,
    IDbContextOutbox outbox,
    IMessageContext messaging,
    TimeProvider? clock = null)
    : WriteDbContext(options, operation, outbox, messaging, clock)
{
    /// <summary>Gets the basket set.</summary>
    public DbSet<Basket> Baskets => Set<Basket>();

    /// <summary>Gets the order set.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>Gets the audit set.</summary>
    public DbSet<OrderAuditEntry> OrderAudit => Set<OrderAuditEntry>();

    /// <summary>Gets the idempotency receipt set.</summary>
    public DbSet<IdempotencyReceipt> IdempotencyReceipts => Set<IdempotencyReceipt>();

    /// <summary>Gets the probe-work set.</summary>
    public DbSet<PlacementProbeWork> PlacementProbeWork => Set<PlacementProbeWork>();

    /// <summary>Gets the provider event receipt set.</summary>
    public DbSet<ProviderEventReceipt> ProviderEventReceipts => Set<ProviderEventReceipt>();

    /// <summary>Gets the reconciliation job set.</summary>
    public DbSet<ReconciliationJob> ReconciliationJobs => Set<ReconciliationJob>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        _ = modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(OrdersWriteDbContext).Assembly,
            ModelConfigurationFilters.WriteConfigurationsFilter);
        base.OnModelCreating(modelBuilder);
    }
}
