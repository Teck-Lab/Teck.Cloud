// <copyright file="WebsiteErrors.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;

namespace Catalog.Domain.Entities.BrandAggregate.Errors
{
    /// <summary>
    /// Contains validation errors related to website properties.
    /// </summary>
    public static class WebsiteErrors
    {
        /// <summary>
        /// Gets error indicating that the website field is empty.
        /// </summary>
        public static Error Empty => Error.Validation(
            "Website.Empty",
            "Website cannot be empty.");

        /// <summary>
        /// Gets error indicating that the website URL is invalid.
        /// </summary>
        public static Error Invalid => Error.Validation(
            "Website.Invalid",
            "Website must be a valid URL starting with http:// or https://.");
    }
}
