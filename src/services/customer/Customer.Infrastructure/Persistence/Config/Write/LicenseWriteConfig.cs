// <copyright file="LicenseWriteConfig.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Domain.Entities.LicenseAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Core.Pricing;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Customer.Infrastructure.Persistence.Config.Write;

/// <summary>
/// Provides Entity Framework configuration for the <see cref="License"/> entity.
/// </summary>
public class LicenseWriteConfig : IEntityTypeConfiguration<License>
{
    /// <summary>
    /// Configures the License entity type.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the License entity.</param>
    public void Configure(EntityTypeBuilder<License> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        ConfigureTable(builder);
        ConfigureMainColumns(builder);
        builder.ConfigureAuditProperties();
    }

    private static void ConfigureTable(EntityTypeBuilder<License> builder)
    {
        builder.ToTable("Licenses");
        builder.HasKey(license => license.Id);
        builder.HasIndex(license => license.TenantId);
        builder.HasIndex(license => new { license.TenantId, license.Status });
        builder.HasIndex(license => license.LocationId);
    }

    private static void ConfigureMainColumns(EntityTypeBuilder<License> builder)
    {
        builder.Property(license => license.TenantId).HasMaxLength(100).IsRequired();
        builder.Property(license => license.LocationId).HasMaxLength(100);
        builder.Property(license => license.Plan).HasMaxLength(50).IsRequired();
        builder.Property(license => license.Status)
            .HasConversion(
                status => status.Name,
                statusName => LicenseStatus.FromName(statusName, false))
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(license => license.LicenseXml).IsRequired();
        builder.Property(license => license.ExpiresAt).IsRequired();
        builder.Property(license => license.GracePeriodEndsAt);
        builder.Property(license => license.PaymentMethodId).HasMaxLength(100);
        builder.Property(license => license.PaymentScope).HasMaxLength(50).IsRequired();
        builder.Property(license => license.OwnershipType)
            .HasConversion(
                ownershipType => ownershipType.Name,
                name => LicenseOwnershipType.FromName(name, false))
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(license => license.UpdatedAt).IsRequired();
    }
}
