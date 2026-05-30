// <copyright file="GetProductItemResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Product.Application.Product.Features.GetProducts.V1;

/// <summary>
/// Response item for a single product.
/// </summary>
/// <param name="ProductId">Unique product identifier.</param>
/// <param name="Name">Product display name.</param>
/// <param name="Sku">Stock-keeping unit code.</param>
/// <param name="Barcode">Product barcode.</param>
public sealed record GetProductItemResponse(
    Guid ProductId,
    string Name,
    string Sku,
    string Barcode);
