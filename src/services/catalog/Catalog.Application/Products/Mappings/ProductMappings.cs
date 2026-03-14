// <copyright file="ProductMappings.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Responses;
using Catalog.Domain.Entities.ProductAggregate;
using Riok.Mapperly.Abstractions;

namespace Catalog.Application.Products.Mappings
{
    /// <summary>
    /// Product mapping definitions.
    /// </summary>
    [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
    internal static partial class ProductMappings
    {
        /// <summary>
        /// Maps a domain product entity to response model.
        /// </summary>
        /// <param name="product">The product entity.</param>
        /// <returns>The product response.</returns>
        internal static partial ProductResponse ProductToProductResponse(Product product);

        /// <summary>
        /// Maps a product read model to response model.
        /// </summary>
        /// <param name="product">The product read model.</param>
        /// <returns>The product response.</returns>
        internal static partial ProductResponse ProductReadModelToProductResponse(ProductReadModel product);
    }
}
