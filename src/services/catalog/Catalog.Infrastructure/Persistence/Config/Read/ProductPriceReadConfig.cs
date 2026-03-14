// <copyright file="ProductPriceReadConfig.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Products.ReadModels;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database.EFCore.Config;

namespace Catalog.Infrastructure.Persistence.Config.Read;

/// <summary>
/// Configuration for <see cref="ProductPriceReadModel"/> entity.
/// </summary>
public class ProductPriceReadConfig : IEntityTypeConfiguration<ProductPriceReadModel>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProductPriceReadModel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProductPrices");

        builder.HasKey(productPrice => productPrice.Id);

        builder.Property(productPrice => productPrice.ProductId)
            .IsRequired();

        builder.Property(productPrice => productPrice.SalePrice)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(productPrice => productPrice.CurrencyCode)
            .HasMaxLength(3);

        builder.Property(productPrice => productPrice.ProductPriceTypeId)
            .IsRequired();

        builder.Property(productPrice => productPrice.ProductPriceTypeName)
            .HasMaxLength(100);

        // Apply standard audit property configurations
        builder.ConfigureAuditProperties();

        builder.IsMultiTenant();
    }
}
