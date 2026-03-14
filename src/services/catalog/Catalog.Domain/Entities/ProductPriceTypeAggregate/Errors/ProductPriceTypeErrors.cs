// <copyright file="ProductPriceTypeErrors.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

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
        "ProductPriceType.NotFound",
        "The specified product price was not found");

    /// <summary>
    /// Gets the not created.
    /// </summary>
    public static Error NotCreated => Error.Failure(
        "ProductPriceType.NotCreated",
        "The product price was not created");

    /// <summary>
    /// Gets the error indicating that the product price type name cannot be empty.
    /// </summary>
    public static Error EmptyName => Error.Validation(
        "ProductPriceType.EmptyName",
        "Product price type name cannot be empty.");

    /// <summary>
    /// Gets the error indicating that the priority cannot be negative.
    /// </summary>
    public static Error NegativePriority => Error.Validation(
        "ProductPriceType.NegativePriority",
        "Priority cannot be negative.");
}
