namespace Catalog.Application.Features.Products.GetProductById.V1
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
