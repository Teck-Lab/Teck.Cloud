// <copyright file="CreateSupplierRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Suppliers.Features.CreateSupplier.V1
{
    /// <summary>
    /// The create supplier request.
    /// </summary>
    public sealed record CreateSupplierRequest
    {
        /// <summary>
        /// Gets or sets the supplier name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the supplier description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the supplier website.
        /// </summary>
        public string? Website { get; set; }
    }
}
