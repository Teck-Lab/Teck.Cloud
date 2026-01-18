using ErrorOr;

namespace Catalog.Domain.Entities.ProductPriceTypeAggregate.Errors;

/// <summary>
/// Provides error definitions for product price type operations.
/// </summary>
public static class ProductPriceTypeErrors
{
    /// <summary>
    /// Gets brand not found error.
    /// </summary>
    public static Error NotFound => Error.NotFound(
        code: "ProductPriceType.NotFound",
        description: "The specified product price was not found");

    /// <summary>
    /// Gets the not created.
    /// </summary>
    public static Error NotCreated => Error.Failure(
        code: "ProductPriceType.NotCreated",
        description: "The product price was not created");

    /// <summary>
    /// Gets the error indicating that the product price type name cannot be empty.
    /// </summary>
    public static Error EmptyName => Error.Validation(
        code: "ProductPriceType.EmptyName",
        description: "Product price type name cannot be empty.");

    /// <summary>
    /// Gets the error indicating that the priority cannot be negative.
    /// </summary>
    public static Error NegativePriority => Error.Validation(
        code: "ProductPriceType.NegativePriority",
        description: "Priority cannot be negative.");
}
