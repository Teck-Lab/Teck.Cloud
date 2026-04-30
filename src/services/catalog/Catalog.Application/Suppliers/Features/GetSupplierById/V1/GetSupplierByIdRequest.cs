// <copyright file="GetSupplierByIdRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Suppliers.Features.GetSupplierById.V1
{
    /// <summary>
    /// The get supplier by id request.
    /// </summary>
    public sealed record GetSupplierByIdRequest
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public Guid Id { get; set; }
    }
}
