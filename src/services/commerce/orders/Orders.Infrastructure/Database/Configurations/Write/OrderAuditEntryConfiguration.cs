using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.OrderAudit;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Database.Configurations.Write;

/// <summary>Write mapping for append-only staff audit rows.</summary>
public sealed class OrderAuditEntryConfiguration : IWriteModelConfiguration<OrderAuditEntry>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OrderAuditEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.ToTable("OrderAudit", "orders");
        _ = builder.HasKey(entry => entry.Id);
        _ = builder.Property(entry => entry.TenantId).HasMaxLength(64).IsRequired();
        _ = builder.Property(entry => entry.ActorId).HasMaxLength(128).IsRequired();
        _ = builder.Property(entry => entry.Reason).HasMaxLength(512);
        _ = builder.HasIndex(entry => new { entry.TenantId, entry.OrderId, entry.OccurredAt });
        _ = builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(entry => entry.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
