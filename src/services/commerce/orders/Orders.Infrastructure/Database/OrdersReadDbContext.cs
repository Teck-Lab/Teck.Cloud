using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.ReadModels;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Database;

/// <summary>The untracked, save-guarded read model.</summary>
/// <param name="options">Provider options.</param>
/// <param name="operation">The scoped operation identity.</param>
/// <param name="clock">Optional application clock.</param>
public sealed class OrdersReadDbContext(
    DbContextOptions<OrdersReadDbContext> options,
    OperationContext operation,
    TimeProvider? clock = null)
    : ScopedDbContext(options, operation, readOnly: true, clock)
{
    /// <summary>Gets the order projection set.</summary>
    public DbSet<OrderReadModel> Orders => Set<OrderReadModel>();

    /// <summary>Gets the audit projection set.</summary>
    public DbSet<OrderAuditReadModel> OrderAudit => Set<OrderAuditReadModel>();

    /// <summary>Gets the basket projection set.</summary>
    public DbSet<BasketReadModel> Baskets => Set<BasketReadModel>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        _ = modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(OrdersWriteDbContext).Assembly,
            ModelConfigurationFilters.ReadConfigurationsFilter);
        base.OnModelCreating(modelBuilder);
    }
}
