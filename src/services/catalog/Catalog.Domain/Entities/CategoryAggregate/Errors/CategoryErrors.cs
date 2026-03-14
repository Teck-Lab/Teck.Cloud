// <copyright file="CategoryErrors.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;

namespace Catalog.Domain.Entities.CategoryAggregate.Errors
{
    /// <summary>
    /// Provides error definitions for category-related operations.
    /// </summary>
    public static class CategoryErrors
    {
        /// <summary>
        /// Gets brand not found error.
        /// </summary>
        public static Error NotFound => Error.NotFound(
            "Category.NotFound",
            "Category not found");

        /// <summary>
        /// Gets category empty name validation error.
        /// </summary>
        public static Error EmptyName => Error.Validation(
            "Category.EmptyName",
            "Category name cannot be empty.");

        /// <summary>
        /// Gets category empty description validation error.
        /// </summary>
        public static Error EmptyDescription => Error.Validation(
            "Category.EmptyDescription",
            "Category description cannot be empty.");
    }
}
