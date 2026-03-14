// <copyright file="DeleteBrandsRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Brands.Features.DeleteBrands.V1
{
    /// <summary>
    /// The delete brands request.
    /// </summary>
    public sealed record DeleteBrandsRequest
    {
        /// <summary>
        /// Gets or sets the ids.
        /// </summary>
        public IReadOnlyCollection<Guid> Ids { get; set; } = [];
    }
}
