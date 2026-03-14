// <copyright file="ProductByIdSpecification.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Ardalis.Specification;

namespace Catalog.Domain.Entities.ProductAggregate.Specifications
{
    /// <summary>
    /// Specification to retrieve a product by its ID.
    /// </summary>
    public sealed class ProductByIdSpecification : Specification<Product>, ISingleResultSpecification<Product>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProductByIdSpecification"/> class.
        /// </summary>
        /// <param name="id">The product ID.</param>
        /// <param name="includeRelations">Whether to include related entities in the query.</param>
        public ProductByIdSpecification(Guid id, bool includeRelations = false)
        {
            this.Query.Where(product => product.Id == id);

            if (includeRelations)
            {
                this.Query
                    .Include(product => product.Brand)
                    .Include(product => product.Categories)
                    .Include(product => product.ProductPrices)
                    .Include(product => product.Promotions);
            }
        }
    }
}
