using Catalog.Domain.Entities.CategoryAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Catalog.Infrastructure.Persistence.Config.Write;

/// <summary>
/// Provides Entity Framework configuration for the <see cref="Category"/> entity.
/// </summary>
public class CategoryWriteConfig : IEntityTypeConfiguration<Category>
{
    /// <summary>
    /// Configures the Category entity type.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the Category entity.</param>
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(category => category.Description)
            .HasMaxLength(1000);

        builder.HasMany(category => category.Products)
            .WithMany(product => product.Categories)
            .UsingEntity(
                "ProductCategories",
                leftBuilder => leftBuilder.HasOne(typeof(Catalog.Domain.Entities.ProductAggregate.Product)).WithMany().HasForeignKey("ProductId"),
                rightBuilder => rightBuilder.HasOne(typeof(Category)).WithMany().HasForeignKey("CategoryId"));

        builder.HasMany(category => category.Promotions)
            .WithMany(promotion => promotion.Categories)
            .UsingEntity(
                "PromotionCategories",
                leftBuilder => leftBuilder.HasOne(typeof(Catalog.Domain.Entities.PromotionAggregate.Promotion)).WithMany().HasForeignKey("PromotionId"),
                rightBuilder => rightBuilder.HasOne(typeof(Category)).WithMany().HasForeignKey("CategoryId"));

        // Apply standard audit property configurations
        builder.ConfigureAuditProperties();
    }
}
