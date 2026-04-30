// <copyright file="GetPaginatedSuppliersRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Pagination;

namespace Catalog.Application.Suppliers.Features.GetPaginatedSuppliers.V1
{
    /// <summary>
    /// The get paginated suppliers request.
    /// </summary>
    public class GetPaginatedSuppliersRequest : PaginationParameters
    {
        /// <summary>
        /// Gets or sets the keyword.
        /// </summary>
        public string? Keyword { get; set; }
    }
}
