using Catalog.Application.Products.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Catalog.Infrastructure.Persistence.Config.Read;

/// <summary>
/// Provides Entity Framework configuration for the <see cref="ProductReadModel"/> entity.
/// </summary>
public class ProductReadConfig : IEntityTypeConfiguration<ProductReadModel>
{
    /// <summary>
    /// Configures the ProductReadModel entity type.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the ProductReadModel entity.</param>
    public void Configure(EntityTypeBuilder<ProductReadModel> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(product => product.Id);

        builder.Property(product => product.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(product => product.Description)
            .HasMaxLength(2000);

        builder.Property(product => product.Sku)
            .HasColumnName("SKU")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(product => product.Sku)
            .IsUnique();

        builder.Property(product => product.BrandId);

        builder.Property(product => product.BrandName)
            .HasMaxLength(200);

        builder.Property(product => product.CategoryId);

        builder.Property(product => product.CategoryName)
            .HasMaxLength(200);

        builder.Property(product => product.SupplierId);

        builder.Property(product => product.SupplierName)
            .HasMaxLength(200);

        builder.Property(product => product.ImageUrl)
            .HasMaxLength(2048);

        // Apply standard audit property configurations
        builder.ConfigureAuditProperties();
    }
}
