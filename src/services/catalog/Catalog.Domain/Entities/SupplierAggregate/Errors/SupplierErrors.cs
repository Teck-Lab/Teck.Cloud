// <copyright file="SupplierErrors.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;

namespace Catalog.Domain.Entities.SupplierAggregate.Errors;

/// <summary>
/// Provides error definitions related to the Supplier entity.
/// </summary>
public static class SupplierErrors
{
    /// <summary>
    /// Gets the error indicating that the supplier was not found.
    /// </summary>
    public static Error NotFound => Error.NotFound(
        "Supplier.NotFound",
        "The specified supplier was not found");

    /// <summary>
    /// Gets the error indicating that the supplier name cannot be empty.
    /// </summary>
    public static Error EmptyName => Error.Validation(
        "Supplier.EmptyName",
        "Supplier name cannot be empty.");

    /// <summary>
    /// Gets the error indicating that the supplier website must be a valid absolute URL.
    /// </summary>
    public static Error InvalidWebsite => Error.Validation(
        "Supplier.InvalidWebsite",
        "Supplier website must be a valid URL.");

    /// <summary>
    /// Gets the error indicating that the supplier website cannot be empty.
    /// </summary>
    public static Error EmptyWebsite => Error.Validation(
        "Supplier.EmptyWebsite",
        "Supplier website cannot be empty.");

    /// <summary>
    /// Gets the error indicating that the supplier description cannot be empty.
    /// </summary>
    public static Error EmptyDescription => Error.Validation(
        "Supplier.EmptyDescription",
        "Supplier description cannot be empty.");
}
