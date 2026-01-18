using ErrorOr;

namespace Catalog.Domain.Entities.ProductAggregate.Errors
{
    /// <summary>
    /// Provides error definitions related to Product operations.
    /// </summary>
    public static class ProductErrors
    {
        /// <summary>
        /// Gets brand not found error.
        /// </summary>
        public static Error NotFound => Error.NotFound(
            code: "Product.NotFound",
            description: "The specified product was not found");

        /// <summary>
        /// Gets the not created.
        /// </summary>
        public static Error NotCreated => Error.Failure(
            code: "Product.NotCreated",
            description: "The product was not created");

        /// <summary>
        /// Gets the error indicating that the product name cannot be empty.
        /// </summary>
        public static Error EmptyName => Error.Validation(
            code: "Product.EmptyName",
            description: "Product name cannot be empty.");

        /// <summary>
        /// Gets the error indicating that the product description cannot be empty.
        /// </summary>
        public static Error EmptyDescription => Error.Validation(
            code: "Product.EmptyDescription",
            description: "Product description cannot be empty.");

        /// <summary>
        /// Gets the error indicating that the product SKU cannot be empty.
        /// </summary>
        public static Error EmptySKU => Error.Validation(
            code: "Product.EmptySKU",
            description: "Product SKU cannot be empty.");

        /// <summary>
        /// Gets the error indicating that the product GTIN cannot be empty.
        /// </summary>
        public static Error EmptyGTIN => Error.Validation(
            code: "Product.EmptyGTIN",
            description: "Product GTIN cannot be empty.");

        /// <summary>
        /// Gets the error indicating that the product must have at least one category.
        /// </summary>
        public static Error EmptyCategories => Error.Validation(
            code: "Product.EmptyCategories",
            description: "Product categories cannot be empty.");

        /// <summary>
        /// Gets the error indicating that the product brand cannot be null.
        /// </summary>
        public static Error NullBrand => Error.Validation(
            code: "Product.NullBrand",
            description: "Product brand cannot be null.");

        /// <summary>
        /// Gets the error indicating that the product must have at least one category.
        /// </summary>
        public static Error NoCategories => Error.Validation(
            code: "Product.NoCategories",
            description: "Product must have at least one category.");

        /// <summary>
        /// Gets the error indicating that the product must have a brand.
        /// </summary>
        public static Error NoBrand => Error.Validation(
            code: "Product.NoBrand",
            description: "Product must have a brand.");
    }
}
