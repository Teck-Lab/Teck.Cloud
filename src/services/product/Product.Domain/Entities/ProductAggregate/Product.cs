// <copyright file="Product.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

#nullable enable

using System.Globalization;
using ErrorOr;
using Product.Domain.Entities.ProductAggregate.Events;
using SharedKernel.Core.Domain;

namespace Product.Domain.Entities.ProductAggregate;

/// <summary>
/// Represents a customer-owned product for display assignments.
/// </summary>
public sealed class Product : BaseEntity, IAggregateRoot
{
    private Product()
    {
    }

    /// <summary>
    /// Gets the product display name.
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Gets the stock-keeping unit code. Must be unique.
    /// </summary>
    public string SKU { get; private set; } = default!;

    /// <summary>
    /// Gets the optional product barcode.
    /// </summary>
    public string? Barcode { get; private set; }

    /// <summary>
    /// Gets the URL-friendly slug generated from the name.
    /// </summary>
    public string Slug { get; private set; } = default!;

    /// <summary>
    /// Gets a value indicating whether the product is active.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Creates a new product.
    /// </summary>
    /// <param name="name">The product name.</param>
    /// <param name="sku">The stock-keeping unit.</param>
    /// <returns>The created product, or validation errors.</returns>
    public static ErrorOr<Product> Create(string name, string sku)
    {
        return Create(name, sku, null);
    }

    /// <summary>
    /// Creates a new product.
    /// </summary>
    /// <param name="name">The product name.</param>
    /// <param name="sku">The stock-keeping unit.</param>
    /// <param name="barcode">The optional barcode.</param>
    /// <returns>The created product, or validation errors.</returns>
    public static ErrorOr<Product> Create(string name, string sku, string? barcode)
    {
        List<Error> errors = Validate(name, sku, barcode);

        if (errors.Count != 0)
        {
            return errors;
        }

        Product product = new()
        {
            Name = name,
            SKU = sku,
            Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode,
            Slug = name.ToLower(CultureInfo.InvariantCulture).Replace(" ", "-", StringComparison.Ordinal),
            IsActive = true,
        };

        product.AddDomainEvent(new ProductCreatedEvent(product.Id, product.Name));

        return product;
    }

    private static List<Error> Validate(string name, string sku, string? barcode)
    {
        List<Error> errors = [];

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(Errors.ProductErrors.EmptyName);
        }
        else if (name.Length > 200)
        {
            errors.Add(Errors.ProductErrors.NameTooLong);
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            errors.Add(Errors.ProductErrors.EmptySKU);
        }
        else if (sku.Length > 100)
        {
            errors.Add(Errors.ProductErrors.SkuTooLong);
        }

        if (!string.IsNullOrEmpty(barcode) && barcode.Length > 50)
        {
            errors.Add(Errors.ProductErrors.BarcodeTooLong);
        }

        return errors;
    }
}
