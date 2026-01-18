using Catalog.Domain.Entities.ProductAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Catalog.Infrastructure.Persistence.Config.Write;

/// <summary>
/// Provides Entity Framework configuration for the <see cref="ProductPrice"/> entity.
/// </summary>
public class ProductPriceWriteConfig : IEntityTypeConfiguration<ProductPrice>
{
    /// <summary>
    /// Configures the ProductPrice entity type.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the ProductPrice entity.</param>
    public void Configure(EntityTypeBuilder<ProductPrice> builder)
    {
        builder.ToTable("ProductPrices");

        builder.HasKey(productPrice => productPrice.Id);

        builder.Property(productPrice => productPrice.SalePrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(productPrice => productPrice.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired();

        builder.HasOne(productPrice => productPrice.Product)
            .WithMany(product => product.ProductPrices)
            .HasForeignKey(productPrice => productPrice.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(productPrice => productPrice.ProductPriceType)
            .WithMany()
            .HasForeignKey(productPrice => productPrice.ProductPriceTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Apply standard audit property configurations
        builder.ConfigureAuditProperties();
    }
}
