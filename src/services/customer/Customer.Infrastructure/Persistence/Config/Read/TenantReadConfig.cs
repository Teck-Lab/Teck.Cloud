using Customer.Application.Tenants.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Customer.Infrastructure.Persistence.Config.Read;

/// <summary>
/// Provides Entity Framework configuration for the <see cref="TenantDto"/> read model.
/// </summary>
public class TenantReadConfig : IEntityTypeConfiguration<TenantDto>
{
    /// <summary>
    /// Configures the TenantDto entity type.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the TenantDto entity.</param>
    public void Configure(EntityTypeBuilder<TenantDto> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(tenant => tenant.Id);

        builder.Property(tenant => tenant.Identifier)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(tenant => tenant.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(tenant => tenant.Plan)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(tenant => tenant.DatabaseStrategy)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(tenant => tenant.DatabaseProvider)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(tenant => tenant.IsActive)
            .IsRequired();

        builder.Property(tenant => tenant.CreatedAt)
            .IsRequired();

        builder.Property(tenant => tenant.UpdatedOn);

        // Ignore collections for now - they will be loaded separately if needed
        builder.Ignore(tenant => tenant.Databases);
        builder.Ignore(tenant => tenant.MigrationStatuses);

        // Read-only queries don't need to track changes
        builder.HasQueryFilter(tenant => !EF.Property<bool>(tenant, "IsDeleted"));
    }
}
