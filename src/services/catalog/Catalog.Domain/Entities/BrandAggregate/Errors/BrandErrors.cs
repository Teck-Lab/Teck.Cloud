// <copyright file="BrandErrors.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;

namespace Catalog.Domain.Entities.BrandAggregate.Errors
{
    /// <summary>
    /// The brand.
    /// </summary>
    public static class BrandErrors
    {
        /// <summary>
        /// Gets brand not found error.
        /// </summary>
        public static Error NotFound => Error.NotFound(
            "Brand.NotFound",
            "The specified brand was not found");

        /// <summary>
        /// Gets brand empty name validation error.
        /// </summary>
        public static Error EmptyName => Error.Validation(
            "Brand.EmptyName",
            "Brand name cannot be empty.");

        /// <summary>
        /// Gets brand empty description validation error.
        /// </summary>
        public static Error EmptyDescription => Error.Validation(
            "Brand.EmptyDescription",
            "Brand description cannot be empty.");

        /// <summary>
        /// Gets brand invalid website validation error.
        /// </summary>
        public static Error InvalidWebsite => Error.Validation(
            "Brand.InvalidWebsite",
            "Brand website must be a valid URL.");

        /// <summary>
        /// Gets brand empty website validation error.
        /// </summary>
        public static Error EmptyWebsite => Error.Validation(
            "Brand.EmptyWebsite",
            "Brand website cannot be empty.");
    }
}
