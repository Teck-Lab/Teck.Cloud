using ErrorOr;

namespace Catalog.Domain.Entities.SupplierAggregate.Errors;

/// <summary>
/// Provides error definitions related to the Supplier entity.
/// </summary>
public static class SupplierErrors
{
    /// <summary>
    /// Gets the error indicating that the supplier name cannot be empty.
    /// </summary>
    public static Error EmptyName => Error.Validation(
        code: "Supplier.EmptyName",
        description: "Supplier name cannot be empty.");

    /// <summary>
    /// Gets the error indicating that the supplier website must be a valid absolute URL.
    /// </summary>
    public static Error InvalidWebsite => Error.Validation(
        code: "Supplier.InvalidWebsite",
        description: "Supplier website must be a valid URL.");

    /// <summary>
    /// Gets the error indicating that the supplier website cannot be empty.
    /// </summary>
    public static Error EmptyWebsite => Error.Validation(
        code: "Supplier.EmptyWebsite",
        description: "Supplier website cannot be empty.");

    /// <summary>
    /// Gets the error indicating that the supplier description cannot be empty.
    /// </summary>
    public static Error EmptyDescription => Error.Validation(
        code: "Supplier.EmptyDescription",
        description: "Supplier description cannot be empty.");
}
