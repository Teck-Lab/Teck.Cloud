using SharedKernel.Core.Domain;

namespace Catalog.Application.Categories.ReadModels;

/// <summary>
/// Read model for Category entities, optimized for queries.
/// </summary>
public class CategoryReadModel : ReadModelBase<Guid>
{
    /// <summary>
    /// Gets or sets the name of the category.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the category.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the parent category ID.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Gets or sets the parent category name.
    /// </summary>
    public string? ParentName { get; set; }

    /// <summary>
    /// Gets or sets the image URL of the category.
    /// </summary>
    public Uri? ImageUrl { get; set; }
}
