using Catalog.Application.Categories.ReadModels;
using Catalog.Application.Categories.Response;
using Catalog.Domain.Entities.CategoryAggregate;
using Riok.Mapperly.Abstractions;

namespace Catalog.Application.Brands.Mappings
{
    [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
    internal static partial class CategoryMapper
    {
        internal static partial CategoryResponse CategoryToCategoryResponse(Category category);

        internal static partial CategoryResponse CategoryReadModelToCategoryResponse(CategoryReadModel category);
    }
}
