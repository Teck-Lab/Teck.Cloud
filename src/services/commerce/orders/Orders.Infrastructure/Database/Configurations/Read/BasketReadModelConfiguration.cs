using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.ReadModels;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Database.Configurations.Read;

/// <summary>Read mapping for the basket projection.</summary>
public sealed class BasketReadModelConfiguration : IReadModelConfiguration<BasketReadModel>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BasketReadModel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.ToTable("Baskets", "orders");
        _ = builder.HasKey(basket => basket.Id);
        _ = builder.Property(basket => basket.TenantId).HasMaxLength(64).IsRequired();
        _ = builder.Property(basket => basket.SubjectId).HasMaxLength(128).IsRequired();
        _ = builder.Property(basket => basket.OfferId).HasMaxLength(64);
        _ = builder.Property(basket => basket.AddOnCodesCsv).HasMaxLength(1024).IsRequired();
        _ = builder.Property(basket => basket.TotalCurrency).HasMaxLength(3);
    }
}
