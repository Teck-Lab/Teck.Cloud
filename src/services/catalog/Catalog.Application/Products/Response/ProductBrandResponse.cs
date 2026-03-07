// <copyright file="ProductBrandResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Products.Responses
{
    /// <summary>
    /// Brand DTO embedded within product responses.
    /// </summary>
    [Serializable]
    public class ProductBrandResponse
    {
        /// <summary>
        /// Gets or sets the brand ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the brand name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the brand description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the brand logo URL.
        /// </summary>
        public Uri? LogoUrl { get; set; }

        /// <summary>
        /// Gets or sets the brand website URL.
        /// </summary>
        public Uri? WebsiteUrl { get; set; }
    }
}
