using Catalog.Application.Categories.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Catalog.Infrastructure.Persistence.Config.Read;

/// <summary>
/// Provides Entity Framework configuration for the <see cref="CategoryReadModel"/> entity.
/// </summary>
public class CategoryReadConfig : IEntityTypeConfiguration<CategoryReadModel>
{
    /// <summary>
    /// Configures the CategoryReadModel entity type.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the CategoryReadModel entity.</param>
    public void Configure(EntityTypeBuilder<CategoryReadModel> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(category => category.Description)
            .HasMaxLength(1000);

        builder.Property(category => category.ParentId);

        builder.Property(category => category.ParentName)
            .HasMaxLength(200);

        builder.Property(category => category.ImageUrl)
            .HasMaxLength(2048);

        // Apply standard audit property configurations
        builder.ConfigureAuditProperties();
    }
}
