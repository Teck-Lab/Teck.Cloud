// <copyright file="ValidateProductsForBasketRpcItemRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Catalog;

/// <summary>
/// Basket line item request for catalog validation RPC.
/// </summary>
public sealed record ValidateProductsForBasketRpcItemRequest
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
