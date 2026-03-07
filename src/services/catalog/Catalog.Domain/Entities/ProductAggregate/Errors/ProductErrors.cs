// <copyright file="ProductErrors.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

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
            "Product.NotFound",
            "The specified product was not found");

        /// <summary>
        /// Gets the not created.
        /// </summary>
        public static Error NotCreated => Error.Failure(
            "Product.NotCreated",
            "The product was not created");

        /// <summary>
        /// Gets the error indicating that the product name cannot be empty.
        /// </summary>
        public static Error EmptyName => Error.Validation(
            "Product.EmptyName",
            "Product name cannot be empty.");

        /// <summary>
        /// Gets the error indicating that the product description cannot be empty.
        /// </summary>
        public static Error EmptyDescription => Error.Validation(
            "Product.EmptyDescription",
            "Product description cannot be empty.");

        /// <summary>
        /// Gets the error indicating that the product SKU cannot be empty.
        /// </summary>
        public static Error EmptySKU => Error.Validation(
            "Product.EmptySKU",
            "Product SKU cannot be empty.");

        /// <summary>
        /// Gets the error indicating that the product GTIN cannot be empty.
        /// </summary>
        public static Error EmptyGTIN => Error.Validation(
            "Product.EmptyGTIN",
            "Product GTIN cannot be empty.");

        /// <summary>
        /// Gets the error indicating that the product must have at least one category.
        /// </summary>
        public static Error EmptyCategories => Error.Validation(
            "Product.EmptyCategories",
            "Product categories cannot be empty.");

        /// <summary>
        /// Gets the error indicating that the product brand cannot be null.
        /// </summary>
        public static Error NullBrand => Error.Validation(
            "Product.NullBrand",
            "Product brand cannot be null.");

        /// <summary>
        /// Gets the error indicating that the product must have at least one category.
        /// </summary>
        public static Error NoCategories => Error.Validation(
            "Product.NoCategories",
            "Product must have at least one category.");

        /// <summary>
        /// Gets the error indicating that the product must have a brand.
        /// </summary>
        public static Error NoBrand => Error.Validation(
            "Product.NoBrand",
            "Product must have a brand.");
    }
}
