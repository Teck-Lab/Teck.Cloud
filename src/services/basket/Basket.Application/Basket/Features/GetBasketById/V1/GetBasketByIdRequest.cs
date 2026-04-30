// <copyright file="GetBasketByIdRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Basket.Application.Basket.Features.GetBasketById.V1;

/// <summary>
/// Request model for fetching basket by id.
/// </summary>
public sealed class GetBasketByIdRequest
{
    /// <summary>
    /// Gets the basket identifier.
    /// </summary>
    public Guid BasketId { get; init; }

    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Gets the customer identifier.
    /// </summary>
    public Guid CustomerId { get; init; }
}
