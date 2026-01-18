using Catalog.Domain.Entities.ProductPriceTypeAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Catalog.Infrastructure.Persistence.Config.Write;

/// <summary>
/// Provides Entity Framework configuration for the <see cref="ProductPriceType"/> entity.
/// </summary>
public class ProductPriceTypeWriteConfig : IEntityTypeConfiguration<ProductPriceType>
{
    /// <summary>
    /// Configures the ProductPriceType entity type.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the ProductPriceType entity.</param>
    public void Configure(EntityTypeBuilder<ProductPriceType> builder)
    {
        builder.ToTable("ProductPriceTypes");

        builder.HasKey(productPriceType => productPriceType.Id);

        builder.Property(productPriceType => productPriceType.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(productPriceType => productPriceType.Priority)
            .IsRequired();

        builder.HasIndex(productPriceType => productPriceType.Name)
            .IsUnique();

        // Apply standard audit property configurations
        builder.ConfigureAuditProperties();
    }
}
