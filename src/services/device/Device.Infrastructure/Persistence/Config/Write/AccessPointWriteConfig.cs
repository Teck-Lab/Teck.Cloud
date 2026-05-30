// <copyright file="AccessPointWriteConfig.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.AccessPoints;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Device.Infrastructure.Persistence.Config.Write;

internal sealed class AccessPointWriteConfig : IEntityTypeConfiguration<AccessPoint>
{
    public void Configure(EntityTypeBuilder<AccessPoint> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("access_points");

        builder.HasKey(accessPoint => accessPoint.Id);

        builder.Property(accessPoint => accessPoint.SerialNumber)
            .HasMaxLength(200)
            .IsRequired();
        builder.HasIndex(accessPoint => accessPoint.SerialNumber)
            .IsUnique()
            .HasDatabaseName("ix_access_points_serial_number_unique");

        builder.Property(accessPoint => accessPoint.Vendor)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(accessPoint => accessPoint.LocationNodeId)
            .HasMaxLength(200)
            .IsRequired();
        builder.HasIndex(accessPoint => new { accessPoint.LocationNodeId, accessPoint.Vendor })
            .HasDatabaseName("ix_access_points_vendor_location");

        builder.Property(accessPoint => accessPoint.Status).IsRequired();
        builder.Property(accessPoint => accessPoint.MaxCapacity).IsRequired();
        builder.Property(accessPoint => accessPoint.CurrentLoad).IsRequired();

        builder.ConfigureAuditProperties();
        builder.IsMultiTenant();
    }
}
