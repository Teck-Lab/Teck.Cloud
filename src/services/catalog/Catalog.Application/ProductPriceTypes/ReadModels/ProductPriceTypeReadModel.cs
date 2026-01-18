using SharedKernel.Core.Domain;

namespace Catalog.Application.ProductPriceTypes.ReadModels;

/// <summary>
/// Read model for ProductPriceType entities, optimized for queries.
/// </summary>
public class ProductPriceTypeReadModel : ReadModelBase<Guid>
{
    /// <summary>
    /// Gets or sets the name of the product price type.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the product price type.
    /// </summary>
    public string? Description { get; set; }
}
