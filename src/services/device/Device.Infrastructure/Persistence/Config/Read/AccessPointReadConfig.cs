// <copyright file="AccessPointReadConfig.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.AccessPoints;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Device.Infrastructure.Persistence.Config.Read;

internal sealed class AccessPointReadConfig : IEntityTypeConfiguration<AccessPoint>
{
    public void Configure(EntityTypeBuilder<AccessPoint> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("access_points");

        builder.HasKey(accessPoint => accessPoint.Id);
        builder.Property(accessPoint => accessPoint.SerialNumber)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(accessPoint => accessPoint.Vendor)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(accessPoint => accessPoint.LocationNodeId)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(accessPoint => accessPoint.Status).IsRequired();
        builder.Property(accessPoint => accessPoint.MaxCapacity).IsRequired();
        builder.Property(accessPoint => accessPoint.CurrentLoad).IsRequired();

        builder.ConfigureAuditProperties();
        builder.IsMultiTenant();
    }
}
