// <copyright file="DeviceLayoutWriteConfig.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DeviceLayoutAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Device.Infrastructure.Persistence.Config.Write;

internal sealed class DeviceLayoutWriteConfig : IEntityTypeConfiguration<DeviceLayout>
{
    public void Configure(EntityTypeBuilder<DeviceLayout> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("device_layouts");

        builder.HasKey(layout => layout.Id);

        builder.Property(layout => layout.DeviceDefinitionId)
            .IsRequired();

        builder.HasIndex(layout => layout.DeviceDefinitionId)
            .HasDatabaseName("ix_device_layouts_device_definition_id");

        builder.Property(layout => layout.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(layout => layout.MaxZoneCount)
            .IsRequired();

        builder.ConfigureAuditProperties();
    }
}
