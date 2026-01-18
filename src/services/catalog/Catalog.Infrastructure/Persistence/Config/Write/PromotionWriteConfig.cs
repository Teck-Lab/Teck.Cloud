using Catalog.Domain.Entities.PromotionAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Catalog.Infrastructure.Persistence.Config.Write;

/// <summary>
/// Provides Entity Framework configuration for the <see cref="Promotion"/> entity.
/// </summary>
public class PromotionWriteConfig : IEntityTypeConfiguration<Promotion>
{
    /// <summary>
    /// Configures the Promotion entity type.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the Promotion entity.</param>
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("Promotions");

        builder.HasKey(promotion => promotion.Id);

        builder.Property(promotion => promotion.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(promotion => promotion.Description)
            .HasMaxLength(1000);

        builder.Property(promotion => promotion.ValidFrom)
            .IsRequired();

        builder.Property(promotion => promotion.ValidTo)
            .IsRequired();

        builder.HasMany(promotion => promotion.Products)
            .WithMany(product => product.Promotions)
            .UsingEntity(
                "ProductPromotions",
                left => left.HasOne(typeof(Catalog.Domain.Entities.ProductAggregate.Product)).WithMany().HasForeignKey("ProductId"),
                right => right.HasOne(typeof(Promotion)).WithMany().HasForeignKey("PromotionId"));

        builder.HasMany(promotion => promotion.Categories)
            .WithMany(category => category.Promotions)
            .UsingEntity(
                "PromotionCategories",
                left => left.HasOne(typeof(Catalog.Domain.Entities.CategoryAggregate.Category)).WithMany().HasForeignKey("CategoryId"),
                right => right.HasOne(typeof(Promotion)).WithMany().HasForeignKey("PromotionId"));

        // Apply standard audit property configurations
        builder.ConfigureAuditProperties();
    }
}
