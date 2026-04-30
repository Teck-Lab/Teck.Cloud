// <copyright file="ProductPriceResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Products.Responses
{
    /// <summary>
    /// The product price response.
    /// </summary>
    public record ProductPriceResponse
    {
        /// <summary>
        /// Gets or sets the sale price.
        /// </summary>
        public decimal SalePrice { get; set; }

        /// <summary>
        /// Gets or sets the currency code.
        /// </summary>
        public string? CurrencyCode { get; set; }
    }
}
