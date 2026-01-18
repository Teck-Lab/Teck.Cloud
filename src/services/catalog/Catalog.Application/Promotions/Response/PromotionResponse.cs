namespace Catalog.Application.Promotions.Response
{
    /// <summary>
    /// The promotion response.
    /// </summary>
    public record PromotionResponse
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; } = default!;

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets the valid from.
        /// </summary>
        public DateTimeOffset ValidFrom { get; private set; } = default!;

        /// <summary>
        /// Gets or sets the valid converts to.
        /// </summary>
        public DateTimeOffset ValidTo { get; set; } = default!;
    }
}
