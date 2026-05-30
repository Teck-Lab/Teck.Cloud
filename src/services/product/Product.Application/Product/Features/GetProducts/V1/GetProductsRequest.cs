// <copyright file="GetProductsRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Pagination;

namespace Product.Application.Product.Features.GetProducts.V1;

/// <summary>
/// Request model for paginated product queries.
/// </summary>
public sealed class GetProductsRequest : PaginationParameters
{
    /// <summary>
    /// Gets or sets the sort column. Allowed values: productId, name, sku.
    /// Defaults to productId when omitted.
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to sort descending.
    /// </summary>
    public bool SortDescending { get; set; }
}
