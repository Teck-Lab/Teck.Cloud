namespace Catalog.Application.Brands.Features.UpdateBrand.V1
{
    /// <summary>
    /// The update brand request.
    /// </summary>
    public sealed class UpdateBrandRequest
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the website.
        /// </summary>
        public string? Website { get; set; }
    }
}
