// <copyright file="DeviceDefinitionWriteConfig.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DeviceDefinitionAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Core.Devices;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Device.Infrastructure.Persistence.Config.Write;

internal sealed class DeviceDefinitionWriteConfig : IEntityTypeConfiguration<DeviceDefinition>
{
    public void Configure(EntityTypeBuilder<DeviceDefinition> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("device_definitions");

        builder.HasKey(definition => definition.Id);

        builder.Property(definition => definition.ModelId)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(definition => definition.ModelId)
            .IsUnique()
            .HasDatabaseName("ix_device_definitions_model_id_unique");

        builder.Property(definition => definition.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(definition => definition.WidthPx);

        builder.Property(definition => definition.HeightPx);

        builder.Property(definition => definition.SupportedColors)
            .IsRequired();

        builder.Property(definition => definition.SupportsNfc)
            .IsRequired();

        builder.Property(definition => definition.EslProvider)
            .HasMaxLength(50)
            .IsRequired()
            .HasConversion(
                eslProvider => eslProvider.Name,
                name => EslProvider.FromName(name, false));

        builder.Property(definition => definition.CatalogManufacturerId);

        builder.Property(definition => definition.CatalogSupplierId);

        builder.Property(definition => definition.CatalogProductId);

        builder.ConfigureAuditProperties();
    }
}
