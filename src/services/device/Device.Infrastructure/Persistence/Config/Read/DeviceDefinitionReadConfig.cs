// <copyright file="DeviceDefinitionReadConfig.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Device.Infrastructure.Persistence.Config.Read;

internal sealed class DeviceDefinitionReadConfig : IEntityTypeConfiguration<DeviceDefinitionReadModel>
{
    public void Configure(EntityTypeBuilder<DeviceDefinitionReadModel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("device_definitions");

        builder.HasKey(model => model.Id);

        builder.Property(model => model.ModelId)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(model => model.ModelId)
            .IsUnique()
            .HasDatabaseName("ix_device_definitions_model_id_unique");

        builder.Property(model => model.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(model => model.WidthPx);

        builder.Property(model => model.HeightPx);

        builder.Property(model => model.SupportedColors)
            .IsRequired();

        builder.Property(model => model.SupportsNfc)
            .IsRequired();

        builder.Property(model => model.EslProvider)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(model => model.CatalogManufacturerId);

        builder.Property(model => model.CatalogSupplierId);

        builder.Property(model => model.CatalogProductId);
    }
}
