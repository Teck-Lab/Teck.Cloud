using SharedKernel.Core.Domain;

namespace Catalog.Application.Brands.ReadModels;

/// <summary>
/// Read model for Brand entities, optimized for queries.
/// </summary>
public class BrandReadModel : ReadModelBase<Guid>
{
    /// <summary>
    /// Gets or sets the name of the brand.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the brand.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the website URL of the brand.
    /// </summary>
    public string? Website { get; set; }
}
