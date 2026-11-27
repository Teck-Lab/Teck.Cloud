using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Baskets;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Database.Configurations.Write;

/// <summary>Write mapping for the single subject basket.</summary>
public sealed class BasketConfiguration : IWriteModelConfiguration<Basket>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Basket> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.ToTable("Baskets", "orders");
        _ = builder.HasKey(basket => basket.Id);
        _ = builder.HasIndex(basket => new { basket.TenantId, basket.SubjectId }).IsUnique();
        _ = builder.Property(basket => basket.TenantId).HasMaxLength(64).IsRequired();
        _ = builder.Property(basket => basket.SubjectId).HasMaxLength(128).IsRequired();
        _ = builder.Property(basket => basket.Revision).IsConcurrencyToken();
        _ = builder.Property(basket => basket.OfferId).HasMaxLength(64);
        _ = builder.Property(basket => basket.AddOnCodesCsv).HasMaxLength(1024).IsRequired();
        _ = builder.Property(basket => basket.TotalCurrency).HasMaxLength(3);
        _ = builder.Ignore(basket => basket.AddOnCodes);
        _ = builder.Ignore(basket => basket.Selection);
        _ = builder.Ignore(basket => basket.Total);
    }
}
