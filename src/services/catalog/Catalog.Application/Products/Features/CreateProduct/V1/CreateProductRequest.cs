namespace Catalog.Application.Products.Features.CreateProduct.V1
{
    /// <summary>
    /// The create product request.
    /// </summary>
    public sealed record CreateProductRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CreateProductRequest"/> class.
        /// </summary>
        public CreateProductRequest()
        {
            CategoryIds = new List<Guid>();
        }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the product SKU.
        /// </summary>
        public string ProductSku { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the GTIN.
        /// </summary>
        public string? GTIN { get; set; }

        /// <summary>
        /// Gets or sets the brand id.
        /// </summary>
        public Guid? BrandId { get; set; }

        /// <summary>
        /// Gets or sets the category ids.
        /// </summary>
        public IEnumerable<Guid> CategoryIds { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the product is active.
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
