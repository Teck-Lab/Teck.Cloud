namespace Catalog.Application.Brands.Features.Responses;

/// <summary>
/// Response DTO for brand data.
/// </summary>
[Serializable]
public class BrandResponse
{
    /// <summary>
    /// Gets or sets the brand ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the brand name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the brand description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the brand logo URL.
    /// </summary>
    public Uri? LogoUrl { get; set; }

    /// <summary>
    /// Gets or sets the brand website URL.
    /// </summary>
    public Uri? WebsiteUrl { get; set; }
}
