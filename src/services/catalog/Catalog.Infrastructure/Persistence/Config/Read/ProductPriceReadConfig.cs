using Catalog.Application.Products.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Catalog.Infrastructure.Persistence.Config.Read;

/// <summary>
/// Configuration for <see cref="ProductPriceReadModel"/> entity.
/// </summary>
public class ProductPriceReadConfig : IEntityTypeConfiguration<ProductPriceReadModel>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProductPriceReadModel> entityTypeBuilder)
    {
        entityTypeBuilder.ToTable("ProductPrices");

        entityTypeBuilder.HasKey(productPrice => productPrice.Id);

        entityTypeBuilder.Property(productPrice => productPrice.ProductId)
            .IsRequired();

        entityTypeBuilder.Property(productPrice => productPrice.SalePrice)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        entityTypeBuilder.Property(productPrice => productPrice.CurrencyCode)
            .HasMaxLength(3);

        entityTypeBuilder.Property(productPrice => productPrice.ProductPriceTypeId)
            .IsRequired();

        entityTypeBuilder.Property(productPrice => productPrice.ProductPriceTypeName)
            .HasMaxLength(100);

        // Apply standard audit property configurations
        entityTypeBuilder.ConfigureAuditProperties();
    }
}
