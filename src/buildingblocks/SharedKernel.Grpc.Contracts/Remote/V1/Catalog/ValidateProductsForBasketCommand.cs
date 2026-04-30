// <copyright file="ValidateProductsForBasketCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FastEndpoints;

namespace SharedKernel.Grpc.Contracts.Remote.V1.Catalog;

/// <summary>
/// Requests basket line validation from the Catalog service.
/// </summary>
public sealed class ValidateProductsForBasketCommand : ICommand<ValidateProductsForBasketRpcResult>
{
    /// <summary>
    /// Gets or sets the downstream service name making the request.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets the requested basket line items to validate.
    /// </summary>
    public IList<ValidateProductsForBasketRpcItemRequest> Items { get; } = [];
}
