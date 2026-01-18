using SharedKernel.Core.Domain;

namespace Catalog.Application.Products.ReadModels;

/// <summary>
/// Read model for Product entities, optimized for queries.
/// </summary>
public class ProductReadModel : ReadModelBase<Guid>
{
    /// <summary>
    /// Gets or sets the name of the product.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the product.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the SKU of the product.
    /// </summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the brand ID of the product.
    /// </summary>
    public Guid? BrandId { get; set; }

    /// <summary>
    /// Gets or sets the brand name of the product.
    /// </summary>
    public string? BrandName { get; set; }

    /// <summary>
    /// Gets or sets the category ID of the product.
    /// </summary>
    public Guid? CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the category name of the product.
    /// </summary>
    public string? CategoryName { get; set; }

    /// <summary>
    /// Gets or sets the supplier ID of the product.
    /// </summary>
    public Guid? SupplierId { get; set; }

    /// <summary>
    /// Gets or sets the supplier name of the product.
    /// </summary>
    public string? SupplierName { get; set; }

    /// <summary>
    /// Gets or sets the image URL of the product.
    /// </summary>
    public Uri? ImageUrl { get; set; }
}
