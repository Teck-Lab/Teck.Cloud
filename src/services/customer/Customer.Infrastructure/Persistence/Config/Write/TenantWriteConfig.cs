using Customer.Domain.Entities.TenantAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Customer.Infrastructure.Persistence.Config.Write;

/// <summary>
/// Provides Entity Framework configuration for the <see cref="Tenant"/> entity.
/// </summary>
public class TenantWriteConfig : IEntityTypeConfiguration<Tenant>
{
    /// <summary>
    /// Configures the Tenant entity type.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the Tenant entity.</param>
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(tenant => tenant.Id);

        builder.Property(tenant => tenant.Identifier)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(tenant => tenant.Identifier)
            .IsUnique();

        builder.Property(tenant => tenant.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(tenant => tenant.Plan)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(tenant => tenant.DatabaseStrategy)
            .HasConversion(
                strategy => strategy.Name,
                strategyName => SharedKernel.Core.Pricing.DatabaseStrategy.FromName(strategyName, false))
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(tenant => tenant.DatabaseProvider)
            .HasConversion(
                provider => provider.Name,
                providerName => SharedKernel.Core.Pricing.DatabaseProvider.FromName(providerName, false))
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(tenant => tenant.IsActive)
            .IsRequired();

        // Configure owned collections
        builder.OwnsMany(tenant => tenant.Databases, databasesBuilder =>
        {
            databasesBuilder.ToTable("TenantDatabaseMetadata");
            databasesBuilder.WithOwner().HasForeignKey(metadata => metadata.TenantId);
            databasesBuilder.HasKey(metadata => new { metadata.TenantId, metadata.ServiceName });

            databasesBuilder.Property(metadata => metadata.ServiceName)
                .HasMaxLength(100)
                .IsRequired();

            databasesBuilder.Property(metadata => metadata.WriteEnvVarKey)
                .HasMaxLength(500)
                .IsRequired();

            databasesBuilder.Property(metadata => metadata.ReadEnvVarKey)
                .HasMaxLength(500);

            databasesBuilder.Property(metadata => metadata.HasSeparateReadDatabase)
                .IsRequired();
            });

        // Apply standard audit property configurations
        builder.ConfigureAuditProperties();
    }
}
