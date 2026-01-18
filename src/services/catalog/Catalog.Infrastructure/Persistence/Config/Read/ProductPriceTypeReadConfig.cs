using Catalog.Application.ProductPriceTypes.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Catalog.Infrastructure.Persistence.Config.Read;

/// <summary>
/// Provides Entity Framework configuration for the <see cref="ProductPriceTypeReadModel"/> entity.
/// </summary>
public class ProductPriceTypeReadConfig : IEntityTypeConfiguration<ProductPriceTypeReadModel>
{
    /// <summary>
    /// Configures the ProductPriceTypeReadModel entity type.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the ProductPriceTypeReadModel entity.</param>
    public void Configure(EntityTypeBuilder<ProductPriceTypeReadModel> builder)
    {
        builder.ToTable("ProductPriceTypes");

        builder.HasKey(productPriceType => productPriceType.Id);

        builder.Property(productPriceType => productPriceType.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(productPriceType => productPriceType.Description)
            .HasMaxLength(500);

        builder.HasIndex(productPriceType => productPriceType.Name)
            .IsUnique();

        // Apply standard audit property configurations
        builder.ConfigureAuditProperties();
    }
}
