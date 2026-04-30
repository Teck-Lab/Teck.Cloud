// <copyright file="ValidateProductsForBasketRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Products.Features.ValidateProductsForBasket.V1;

/// <summary>
/// Request payload for validating basket line items against the catalog.
/// </summary>
public sealed class ValidateProductsForBasketRequest
{
    /// <summary>
    /// Gets the line items to validate.
    /// </summary>
    public IList<ValidateProductsForBasketItemRequest> Items { get; } = [];
}

/// <summary>
/// Basket line item validation request.
/// </summary>
public sealed record ValidateProductsForBasketItemRequest
{
    /// <summary>
    /// Gets or sets the product identifier.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Gets or sets the requested quantity.
    /// </summary>
    public int Quantity { get; set; }
}
