// <copyright file="CreateOrderFromBasketRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Order.Application.Orders.Features.CreateOrderFromBasket.V1;

/// <summary>
/// Request model for creating order from basket.
/// </summary>
public sealed class CreateOrderFromBasketRequest
{
    /// <summary>
    /// Gets tenant identifier.
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Gets customer identifier.
    /// </summary>
    public Guid CustomerId { get; init; }

    /// <summary>
    /// Gets basket identifier.
    /// </summary>
    public Guid BasketId { get; init; }
}
