using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.ProviderEvent;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Database.Configurations.Write;

/// <summary>Write mapping for the global provider event receipt ledger.</summary>
public sealed class ProviderEventReceiptConfiguration : IWriteModelConfiguration<ProviderEventReceipt>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProviderEventReceipt> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.ToTable("ProviderEventReceipts", "orders");
        _ = builder.HasKey(receipt => receipt.Id);
        _ = builder.Property(receipt => receipt.AccountId).HasMaxLength(128).IsRequired();
        _ = builder.Property(receipt => receipt.EventId).HasMaxLength(256).IsRequired();
        _ = builder.Property(receipt => receipt.EventType).HasMaxLength(128).IsRequired();
        _ = builder.Property(receipt => receipt.PayloadHash).HasMaxLength(64).IsRequired();
        _ = builder.Property(receipt => receipt.TenantId).HasMaxLength(64);
        _ = builder.Property(receipt => receipt.LastErrorCategory).HasMaxLength(64);
        _ = builder.HasIndex(receipt => new { receipt.AccountId, receipt.EventId }).IsUnique();
        _ = builder.HasIndex(receipt => receipt.OrderId);
    }
}
