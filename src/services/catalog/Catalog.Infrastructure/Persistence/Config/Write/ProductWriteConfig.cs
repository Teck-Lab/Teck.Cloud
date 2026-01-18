using Catalog.Domain.Entities.ProductAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Catalog.Infrastructure.Persistence.Config.Write;

/// <summary>
/// Provides Entity Framework configuration for the <see cref="Product"/> entity.
/// </summary>
public class ProductWriteConfig : IEntityTypeConfiguration<Product>
{
    /// <summary>
    /// Configures the Product entity type.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the Product entity.</param>
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(product => product.Id);

        builder.Property(product => product.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(product => product.Description)
            .HasMaxLength(2000);

        builder.Property(product => product.Slug)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(product => product.IsActive)
            .HasDefaultValue(true);

        builder.Property(product => product.ProductSKU)
            .HasColumnName("SKU")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(product => product.ProductSKU)
            .IsUnique();

        builder.Property(product => product.GTIN)
            .HasMaxLength(14);

        builder.HasOne(product => product.Brand)
            .WithMany(brand => brand.Products)
            .HasForeignKey(product => product.BrandId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(product => product.Categories)
            .WithMany(category => category.Products)
            .UsingEntity(
                "ProductCategories",
                leftBuilder => leftBuilder.HasOne(typeof(Catalog.Domain.Entities.CategoryAggregate.Category)).WithMany().HasForeignKey("CategoryId"),
                rightBuilder => rightBuilder.HasOne(typeof(Product)).WithMany().HasForeignKey("ProductId"));

        builder.HasMany(product => product.Promotions)
            .WithMany(promotion => promotion.Products)
            .UsingEntity(
                "ProductPromotions",
                leftBuilder => leftBuilder.HasOne(typeof(Catalog.Domain.Entities.PromotionAggregate.Promotion)).WithMany().HasForeignKey("PromotionId"),
                rightBuilder => rightBuilder.HasOne(typeof(Product)).WithMany().HasForeignKey("ProductId"));

        // Apply standard audit property configurations
        builder.ConfigureAuditProperties();
    }
}
