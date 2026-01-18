using Catalog.Domain.Entities.BrandAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Catalog.Infrastructure.Persistence.Config.Write;

/// <summary>
/// Provides Entity Framework configuration for the <see cref="Brand"/> entity.
/// </summary>
public class BrandWriteConfig : IEntityTypeConfiguration<Brand>
{
    /// <summary>
    /// Configures the Brand entity type.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the Brand entity.</param>
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");

        builder.HasKey(brand => brand.Id);

        builder.Property(brand => brand.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(brand => brand.Description)
            .HasMaxLength(1000);

        builder.OwnsOne(brand => brand.Website, website =>
        {
            website.Property(websiteEntity => websiteEntity.Value)
                .HasColumnName("Website")
                .HasMaxLength(2048);
        });

        builder.HasMany(brand => brand.Products)
            .WithOne(product => product.Brand)
            .HasForeignKey(product => product.BrandId)
            .OnDelete(DeleteBehavior.SetNull);

        // Apply standard audit property configurations
        builder.ConfigureAuditProperties();
    }
}
