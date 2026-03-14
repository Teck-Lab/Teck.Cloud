// <copyright file="CategoryMappings.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Categories.ReadModels;
using Catalog.Application.Categories.Response;
using Catalog.Domain.Entities.CategoryAggregate;
using Riok.Mapperly.Abstractions;

namespace Catalog.Application.Categories.Mappings
{
    /// <summary>
    /// Category mapping definitions.
    /// </summary>
    [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
    internal static partial class CategoryMapper
    {
        /// <summary>
        /// Maps a domain category entity to response model.
        /// </summary>
        /// <param name="category">The category entity.</param>
        /// <returns>The category response.</returns>
        internal static partial CategoryResponse CategoryToCategoryResponse(Category category);

        /// <summary>
        /// Maps a category read model to response model.
        /// </summary>
        /// <param name="category">The category read model.</param>
        /// <returns>The category response.</returns>
        internal static partial CategoryResponse CategoryReadModelToCategoryResponse(CategoryReadModel category);
    }
}
