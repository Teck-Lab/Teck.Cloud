using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Idempotency;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Database.Configurations.Write;

/// <summary>Write mapping for durable checkout intents.</summary>
public sealed class IdempotencyReceiptConfiguration : IWriteModelConfiguration<IdempotencyReceipt>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<IdempotencyReceipt> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.ToTable("IdempotencyReceipts", "orders");
        _ = builder.HasKey(receipt => receipt.Id);
        _ = builder.Property(receipt => receipt.TenantId).HasMaxLength(64).IsRequired();
        _ = builder.Property(receipt => receipt.SubjectId).HasMaxLength(128).IsRequired();
        _ = builder.Property(receipt => receipt.IdempotencyKey).HasMaxLength(36).IsRequired();
        _ = builder.HasIndex(receipt => new { receipt.TenantId, receipt.SubjectId, receipt.IdempotencyKey }).IsUnique();
        _ = builder.HasIndex(receipt => receipt.OrderId);
    }
}
