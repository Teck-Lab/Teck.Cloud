using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Responses;
using Catalog.Domain.Entities.ProductAggregate;
using Riok.Mapperly.Abstractions;

namespace Catalog.Application.Products.Mappings
{
    [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
    internal static partial class ProductMappings
    {
        internal static partial ProductResponse ProductToProductResponse(Product product);

        internal static partial ProductResponse ProductReadModelToProductResponse(ProductReadModel product);
    }
}
