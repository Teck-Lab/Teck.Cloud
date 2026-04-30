// <copyright file="GetProductByIdRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Products.Features.GetProductById.V1
{
    /// <summary>
    /// The get product by id request.
    /// </summary>
    public sealed record GetProductByIdRequest
    {
        /// <summary>
        /// Gets or sets the product id.
        /// </summary>
        public Guid ProductId { get; set; }
    }
}
