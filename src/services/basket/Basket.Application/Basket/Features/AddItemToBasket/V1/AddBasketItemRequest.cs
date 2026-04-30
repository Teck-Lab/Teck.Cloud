// <copyright file="AddBasketItemRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Basket.Application.Basket.Features.AddItemToBasket.V1;

/// <summary>
/// Request model for adding an item to basket.
/// </summary>
public sealed class AddBasketItemRequest
{
    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Gets the customer identifier.
    /// </summary>
    public Guid CustomerId { get; init; }

    /// <summary>
    /// Gets the product identifier.
    /// </summary>
    public Guid ProductId { get; init; }

    /// <summary>
    /// Gets the quantity to add.
    /// </summary>
    public int Quantity { get; init; }
}
