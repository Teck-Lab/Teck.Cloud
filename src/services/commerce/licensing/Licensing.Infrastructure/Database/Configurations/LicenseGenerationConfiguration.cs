using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGenerations;

namespace Teck.Cloud.Commerce.Licensing.Infrastructure.Database.Configurations;

/// <summary>Maps the durable generation fence and monotonic license revision.</summary>
public sealed class LicenseGenerationConfiguration : ISharedModelConfiguration<LicenseGeneration>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LicenseGeneration> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.ToTable("license_generations", "licensing");
        _ = builder.HasKey(generation => generation.Id);
        _ = builder.Property(generation => generation.Id).HasColumnName("id").ValueGeneratedNever().IsRequired();
        _ = builder.Property(generation => generation.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        _ = builder.Property(generation => generation.OrderId).HasColumnName("order_id").IsRequired();
        _ = builder.Property(generation => generation.SubscriptionId).HasColumnName("subscription_id").IsRequired();
        _ = builder.Property(generation => generation.DesiredRevision).HasColumnName("desired_revision").HasConversion(value => (long)value, value => (uint)value).IsRequired();
        _ = builder.Property(generation => generation.LicenseRevision).HasColumnName("license_revision").HasConversion(value => (long)value, value => (uint)value).IsRequired();
        _ = builder.Property(generation => generation.Fence).HasColumnName("fence").HasConversion(value => (long)value, value => (uint)value).IsConcurrencyToken().IsRequired();
        _ = builder.Property(generation => generation.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
        _ = builder.Property(generation => generation.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(value: false).IsRequired();

        _ = builder.Ignore(generation => generation.CreatedAt);
        _ = builder.Ignore(generation => generation.CreatedBy);
        _ = builder.Ignore(generation => generation.UpdatedOn);
        _ = builder.Ignore(generation => generation.UpdatedBy);
        _ = builder.Ignore(generation => generation.DeletedOn);
        _ = builder.Ignore(generation => generation.DeletedBy);

        _ = builder.HasIndex(generation => new { generation.TenantId, generation.OrderId, generation.SubscriptionId })
            .IsUnique()
            .HasDatabaseName("ux_license_generations_binding");
    }
}
