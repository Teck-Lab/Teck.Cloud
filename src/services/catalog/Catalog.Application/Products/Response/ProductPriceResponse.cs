namespace Catalog.Application.Features.ProductPrices.Response
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
