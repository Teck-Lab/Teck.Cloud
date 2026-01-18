using SharedKernel.Core.Domain;

namespace Catalog.Application.Suppliers.ReadModels;

/// <summary>
/// Read model for Supplier entities, optimized for queries.
/// </summary>
public class SupplierReadModel : ReadModelBase<Guid>
{
    /// <summary>
    /// Gets or sets the name of the supplier.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the supplier.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the contact email of the supplier.
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// Gets or sets the contact phone of the supplier.
    /// </summary>
    public string? ContactPhone { get; set; }

    /// <summary>
    /// Gets or sets the contact name of the supplier.
    /// </summary>
    public string? ContactName { get; set; }

    /// <summary>
    /// Gets or sets the website URL of the supplier.
    /// </summary>
    public Uri? WebsiteUrl { get; set; }
}
