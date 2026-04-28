// <copyright file="GetPaginatedProductsResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Products.Features.GetPaginatedProducts.V1;

/// <summary>
/// Response model for paginated product list items.
/// </summary>
[Serializable]
public sealed class GetPaginatedProductsResponse
{
    /// <summary>
    /// Gets or sets the product identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the product name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the product description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the product SKU.
    /// </summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the brand identifier.
    /// </summary>
    public Guid? BrandId { get; set; }

    /// <summary>
    /// Gets or sets the brand name.
    /// </summary>
    public string? BrandName { get; set; }

    /// <summary>
    /// Gets or sets the category identifier.
    /// </summary>
    public Guid? CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the category name.
    /// </summary>
    public string? CategoryName { get; set; }

    /// <summary>
    /// Gets or sets the supplier identifier.
    /// </summary>
    public Guid? SupplierId { get; set; }

    /// <summary>
    /// Gets or sets the supplier name.
    /// </summary>
    public string? SupplierName { get; set; }

    /// <summary>
    /// Gets or sets the product image URL.
    /// </summary>
    public Uri? ImageUrl { get; set; }
}
