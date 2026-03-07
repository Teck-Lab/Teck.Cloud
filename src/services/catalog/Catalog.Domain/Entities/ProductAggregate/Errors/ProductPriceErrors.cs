// <copyright file="ProductPriceErrors.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

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
        "ProductPrice.NotFound",
        "The specified product price was not found");

    /// <summary>
    /// Gets the not created.
    /// </summary>
    public static Error NotCreated => Error.Failure(
        "ProductPrice.NotCreated",
        "The product price was not created");

    /// <summary>
    /// Gets the error indicating that the sale price is negative.
    /// </summary>
    public static Error NegativePrice => Error.Validation(
        "ProductPrice.NegativePrice",
        "Sale price cannot be negative.");

    /// <summary>
    /// Gets the error indicating that the currency code is empty.
    /// </summary>
    public static Error EmptyCurrencyCode => Error.Validation(
        "ProductPrice.EmptyCurrencyCode",
        "Currency code cannot be empty.");

    /// <summary>
    /// Gets the error indicating that the ProductId is the default value.
    /// </summary>
    public static Error DefaultProductId => Error.Validation(
        "ProductPrice.DefaultProductId",
        "ProductId cannot be the default value.");

    /// <summary>
    /// Gets the error indicating that the ProductPriceTypeId is the default value.
    /// </summary>
    public static Error DefaultProductPriceTypeId => Error.Validation(
        "ProductPrice.DefaultProductPriceTypeId",
        "ProductPriceTypeId cannot be the default value.");
}
