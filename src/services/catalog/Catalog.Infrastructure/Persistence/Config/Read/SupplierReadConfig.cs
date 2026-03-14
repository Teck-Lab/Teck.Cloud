// <copyright file="SupplierReadConfig.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Suppliers.ReadModels;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Catalog.Infrastructure.Persistence.Config.Read;

/// <summary>
/// Configuration for <see cref="SupplierReadModel"/> entity.
/// </summary>
public class SupplierReadConfig : IEntityTypeConfiguration<SupplierReadModel>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SupplierReadModel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Suppliers");

        builder.HasKey(supplier => supplier.Id);

        builder.Property(supplier => supplier.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(supplier => supplier.Description)
            .HasMaxLength(500);

        builder.Property(supplier => supplier.WebsiteUrl)
            .HasConversion(
                value => value == null ? null : value.ToString(),
                value => string.IsNullOrWhiteSpace(value) ? null : new Uri(value, UriKind.RelativeOrAbsolute))
            .HasMaxLength(255);

        builder.Property(supplier => supplier.ContactEmail)
            .HasMaxLength(100);

        builder.Property(supplier => supplier.ContactName)
            .HasMaxLength(100);

        builder.Property(supplier => supplier.ContactPhone)
            .HasMaxLength(20);

        // Apply standard audit property configurations
        builder.ConfigureAuditProperties();

        builder.IsMultiTenant();
    }
}
