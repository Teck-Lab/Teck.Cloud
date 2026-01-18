using Catalog.Application.Promotions.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Catalog.Infrastructure.Persistence.Config.Read;

/// <summary>
/// Provides Entity Framework configuration for the <see cref="PromotionReadModel"/> entity.
/// </summary>
public class PromotionReadConfig : IEntityTypeConfiguration<PromotionReadModel>
{
    /// <summary>
    /// Configures the PromotionReadModel entity type.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the PromotionReadModel entity.</param>
    public void Configure(EntityTypeBuilder<PromotionReadModel> builder)
    {
        builder.ToTable("Promotions");

        builder.HasKey(promotion => promotion.Id);

        builder.Property(promotion => promotion.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(promotion => promotion.Description)
            .HasMaxLength(1000);

        builder.Property(promotion => promotion.DiscountPercentage)
            .HasPrecision(5, 2);

        builder.Property(promotion => promotion.StartDate)
            .HasColumnName("ValidFrom")
            .IsRequired();

        builder.Property(promotion => promotion.EndDate)
            .HasColumnName("ValidTo")
            .IsRequired();

        builder.Property(promotion => promotion.IsActive);

        // Apply standard audit property configurations
        builder.ConfigureAuditProperties();
    }
}
