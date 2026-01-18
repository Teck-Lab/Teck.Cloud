using SharedKernel.Core.Domain;

namespace Catalog.Application.Products.ReadModels;

/// <summary>
/// Read model for ProductPrice entities, optimized for queries.
/// </summary>
public class ProductPriceReadModel : ReadModelBase<Guid>
{
    /// <summary>
    /// Gets or sets the product id.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Gets or sets the sale price without VAT.
    /// </summary>
    public decimal SalePrice { get; set; }

    /// <summary>
    /// Gets or sets the currency code.
    /// </summary>
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// Gets or sets the product price type id.
    /// </summary>
    public Guid ProductPriceTypeId { get; set; }

    /// <summary>
    /// Gets or sets the product price type name.
    /// </summary>
    public string? ProductPriceTypeName { get; set; }
}
