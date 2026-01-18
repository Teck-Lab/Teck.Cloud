namespace Catalog.Application.Brands.Features.GetBrandById.V1
{
    /// <summary>
    /// The get brand request.
    /// </summary>
    public sealed record GetBrandByIdRequest
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public Guid Id { get; set; }
    }
}
