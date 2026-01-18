using ErrorOr;

namespace Catalog.Domain.Entities.ProductAggregate.Errors;

/// <summary>
/// Provides error definitions related to product price operations.
/// </summary>
public static class ProductPriceErrors
{
    /// <summary>
    /// Gets brand not found error.
    /// </summary>
    public static Error NotFound => Error.NotFound(
        code: "ProductPrice.NotFound",
        description: "The specified product price was not found");

    /// <summary>
    /// Gets the not created.
    /// </summary>
    public static Error NotCreated => Error.Failure(
        code: "ProductPrice.NotCreated",
        description: "The product price was not created");

    /// <summary>
    /// Gets the error indicating that the sale price is negative.
    /// </summary>
    public static Error NegativePrice => Error.Validation(
        code: "ProductPrice.NegativePrice",
        description: "Sale price cannot be negative.");

    /// <summary>
    /// Gets the error indicating that the currency code is empty.
    /// </summary>
    public static Error EmptyCurrencyCode => Error.Validation(
        code: "ProductPrice.EmptyCurrencyCode",
        description: "Currency code cannot be empty.");

    /// <summary>
    /// Gets the error indicating that the ProductId is the default value.
    /// </summary>
    public static Error DefaultProductId => Error.Validation(
        code: "ProductPrice.DefaultProductId",
        description: "ProductId cannot be the default value.");

    /// <summary>
    /// Gets the error indicating that the ProductPriceTypeId is the default value.
    /// </summary>
    public static Error DefaultProductPriceTypeId => Error.Validation(
        code: "ProductPrice.DefaultProductPriceTypeId",
        description: "ProductPriceTypeId cannot be the default value.");
}
