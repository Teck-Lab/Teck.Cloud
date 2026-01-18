using Catalog.Application.Brands.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Catalog.Infrastructure.Persistence.Config.Read;

/// <summary>
/// Provides Entity Framework configuration for the <see cref="BrandReadModel"/> entity.
/// </summary>
public class BrandReadConfig : IEntityTypeConfiguration<BrandReadModel>
{
    /// <summary>
    /// Configures the BrandReadModel entity type.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the BrandReadModel entity.</param>
    public void Configure(EntityTypeBuilder<BrandReadModel> builder)
    {
        builder.ToTable("Brands");

        builder.HasKey(brand => brand.Id);

        builder.Property(brand => brand.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(brand => brand.Description)
            .HasMaxLength(1000);

        builder.Property(brand => brand.Website)
            .HasMaxLength(2048);

        // Apply standard audit property configurations
        builder.ConfigureAuditProperties();
    }
}
