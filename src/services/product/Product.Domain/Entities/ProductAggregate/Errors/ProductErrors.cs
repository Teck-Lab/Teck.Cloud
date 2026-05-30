// <copyright file="ProductErrors.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;

namespace Product.Domain.Entities.ProductAggregate.Errors;

/// <summary>
/// Provides error definitions related to Product operations.
/// </summary>
public static class ProductErrors
{
    /// <summary>
    /// Gets the not found error.
    /// </summary>
    public static Error NotFound => Error.NotFound(
        "Product.NotFound",
        "The specified product was not found.");

    /// <summary>
    /// Gets the error indicating that the product name cannot be empty.
    /// </summary>
    public static Error EmptyName => Error.Validation(
        "Product.EmptyName",
        "Product name cannot be empty.");

    /// <summary>
    /// Gets the error indicating that the product SKU cannot be empty.
    /// </summary>
    public static Error EmptySKU => Error.Validation(
        "Product.EmptySKU",
        "Product SKU cannot be empty.");

    /// <summary>
    /// Gets the error indicating that the product name exceeds the maximum length.
    /// </summary>
    public static Error NameTooLong => Error.Validation(
        "Product.NameTooLong",
        "Product name cannot exceed 200 characters.");

    /// <summary>
    /// Gets the error indicating that the product SKU exceeds the maximum length.
    /// </summary>
    public static Error SkuTooLong => Error.Validation(
        "Product.SkuTooLong",
        "Product SKU cannot exceed 100 characters.");

    /// <summary>
    /// Gets the error indicating that the product barcode exceeds the maximum length.
    /// </summary>
    public static Error BarcodeTooLong => Error.Validation(
        "Product.BarcodeTooLong",
        "Product barcode cannot exceed 50 characters.");

    /// <summary>
    /// Gets the error indicating that a product with the same SKU already exists.
    /// </summary>
    public static Error DuplicateSku => Error.Conflict(
        "Product.DuplicateSku",
        "A product with the same SKU already exists.");
}
