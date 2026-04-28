// <copyright file="GetPaginatedProductsRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Pagination;

namespace Catalog.Application.Products.Features.GetPaginatedProducts.V1
{
    /// <summary>
    /// Request model for paginated product queries.
    /// </summary>
    public class GetPaginatedProductsRequest : PaginationParameters
    {
        /// <summary>
        /// Gets or sets optional keyword filter for name/description.
        /// </summary>
        public string? Keyword { get; set; }
    }
}
