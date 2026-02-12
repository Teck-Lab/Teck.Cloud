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

            databasesBuilder.Property(metadata => metadata.VaultWritePath)
                .HasMaxLength(500)
                .IsRequired();

            databasesBuilder.Property(metadata => metadata.VaultReadPath)
                .HasMaxLength(500);

            databasesBuilder.Property(metadata => metadata.HasSeparateReadDatabase)
                .IsRequired();
        });

        builder.OwnsMany(tenant => tenant.MigrationStatuses, statusesBuilder =>
        {
            statusesBuilder.ToTable("TenantMigrationStatuses");
            statusesBuilder.WithOwner().HasForeignKey(status => status.TenantId);
            statusesBuilder.HasKey(status => new { status.TenantId, status.ServiceName });

            statusesBuilder.Property(status => status.ServiceName)
                .HasMaxLength(100)
                .IsRequired();

            statusesBuilder.Property(status => status.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            statusesBuilder.Property(status => status.LastMigrationVersion)
                .HasMaxLength(100);

            statusesBuilder.Property(status => status.ErrorMessage)
                .HasMaxLength(2000);

            statusesBuilder.Property(status => status.StartedAt);

            statusesBuilder.Property(status => status.CompletedAt);
        });

        // Apply standard audit property configurations
        builder.ConfigureAuditProperties();
    }
}
