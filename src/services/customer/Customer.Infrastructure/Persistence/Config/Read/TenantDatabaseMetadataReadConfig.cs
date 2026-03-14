// <copyright file="TenantDatabaseMetadataReadConfig.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Customer.Infrastructure.Persistence.Config.Read;

/// <summary>
/// Provides Entity Framework configuration for tenant database metadata read rows.
/// </summary>
public sealed class TenantDatabaseMetadataReadConfig : IEntityTypeConfiguration<TenantDatabaseMetadataReadModel>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TenantDatabaseMetadataReadModel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TenantDatabaseMetadata");
        builder.HasKey(metadata => new { metadata.TenantId, metadata.ServiceName });

        builder.Property(metadata => metadata.TenantId).IsRequired();
        builder.Property(metadata => metadata.ServiceName).HasMaxLength(100).IsRequired();
        builder.Property(metadata => metadata.ReadDatabaseMode).IsRequired();
        builder.Property(metadata => metadata.IsDeleted).IsRequired();
    }
}
