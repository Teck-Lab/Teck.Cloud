// <copyright file="CategoriesByIdsSpecification.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Ardalis.Specification;

namespace Catalog.Domain.Entities.CategoryAggregate.Specifications
{
    /// <summary>
    /// Specification to retrieve a category by its ID.
    /// </summary>
    public sealed class CategoriesByIdsSpecification : Specification<Category>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CategoriesByIdsSpecification"/> class.
        /// </summary>
        /// <param name="ids">The category IDs.</param>
        public CategoriesByIdsSpecification(IReadOnlyCollection<Guid> ids)
        {
            this.Query.Where(category => ids.Contains(category.Id));
        }
    }
}
