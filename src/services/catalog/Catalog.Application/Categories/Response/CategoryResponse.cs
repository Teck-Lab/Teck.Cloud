namespace Catalog.Application.Categories.Response
{
    /// <summary>
    /// The category response.
    /// </summary>
    [Serializable]
    public record CategoryResponse
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string? Description { get; set; }
    }
}
