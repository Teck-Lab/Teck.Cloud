// <copyright file="DisplayWriteConfig.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DisplayAggregate;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Device.Infrastructure.Persistence.Config.Write;

internal sealed class DisplayWriteConfig : IEntityTypeConfiguration<Display>
{
    public void Configure(EntityTypeBuilder<Display> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("displays");

        builder.HasKey(display => display.Id);

        builder.Property(display => display.ShortSerial)
            .HasMaxLength(11) // "XX-XX-XX-XX"
            .IsRequired();

        builder.HasIndex(display => display.ShortSerial)
            .IsUnique()
            .HasDatabaseName("ix_displays_short_serial_unique");

        builder.Property(display => display.LongSerial);

        builder.Property(display => display.LocationNodeId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(display => display.DeviceDefinitionId);

        builder.Property(display => display.DeviceLayoutId);

        builder.Property(display => display.CurrentAssignmentId);

        builder.ConfigureAuditProperties();

        builder.IsMultiTenant();
    }
}
