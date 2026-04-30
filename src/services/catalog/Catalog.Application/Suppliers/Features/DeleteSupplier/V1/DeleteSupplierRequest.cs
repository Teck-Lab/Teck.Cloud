// <copyright file="DeleteSupplierRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Suppliers.Features.DeleteSupplier.V1
{
    /// <summary>
    /// The delete supplier request.
    /// </summary>
    public sealed record DeleteSupplierRequest
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public Guid Id { get; set; }
    }
}
