// <copyright file="DeviceLayoutReadConfig.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceLayouts.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Device.Infrastructure.Persistence.Config.Read;

internal sealed class DeviceLayoutReadConfig : IEntityTypeConfiguration<DeviceLayoutReadModel>
{
    public void Configure(EntityTypeBuilder<DeviceLayoutReadModel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("device_layouts");

        builder.HasKey(model => model.Id);

        builder.Property(model => model.DeviceDefinitionId)
            .IsRequired();

        builder.HasIndex(model => model.DeviceDefinitionId)
            .HasDatabaseName("ix_device_layouts_device_definition_id");

        builder.Property(model => model.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(model => model.MaxZoneCount)
            .IsRequired();

        builder.Property(model => model.CreatedAt)
            .IsRequired();

        builder.Property(model => model.CreatedBy)
            .HasMaxLength(100);

        builder.Property(model => model.UpdatedOn);

        builder.Property(model => model.UpdatedBy)
            .HasMaxLength(100);

        builder.Property(model => model.DeletedOn);

        builder.Property(model => model.DeletedBy)
            .HasMaxLength(100);

        builder.Property(model => model.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);
    }
}
