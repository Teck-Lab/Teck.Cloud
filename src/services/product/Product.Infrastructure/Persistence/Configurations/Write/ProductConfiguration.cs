// <copyright file="ProductConfiguration.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Product.Infrastructure.Persistence.Configurations.Write;

/// <summary>
/// EF Core entity configuration for <see cref="Domain.Entities.ProductAggregate.Product"/> on the write side.
/// </summary>
internal sealed class ProductConfiguration : IEntityTypeConfiguration<Domain.Entities.ProductAggregate.Product>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Domain.Entities.ProductAggregate.Product> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Products");

        builder.HasKey(product => product.Id);

        builder.Property(product => product.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(product => product.SKU)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(product => product.Barcode)
            .HasMaxLength(50);

        builder.Property(product => product.Slug)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(product => product.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(product => product.SKU)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}
