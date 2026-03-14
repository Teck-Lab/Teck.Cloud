// <copyright file="SupplierWriteConfig.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Domain.Entities.SupplierAggregate;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Catalog.Infrastructure.Persistence.Config.Write;

/// <summary>
/// Configuration for <see cref="Supplier"/> entity.
/// </summary>
public class SupplierWriteConfig : IEntityTypeConfiguration<Supplier>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Suppliers");

        builder.HasKey(supplier => supplier.Id);

        builder.Property(supplier => supplier.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(supplier => supplier.Description)
            .HasMaxLength(500);

        builder.Property(supplier => supplier.Website)
            .HasMaxLength(255);

        // Apply standard audit property configurations
        builder.ConfigureAuditProperties();

        builder.IsMultiTenant();
    }
}
