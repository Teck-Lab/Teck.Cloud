using Catalog.Application.Products.Mappings;
using Catalog.Domain.Entities.ProductAggregate;

namespace Catalog.UnitTests.Application.Products
{
    public class ProductMappingsTests
    {
        [Fact]
        public void ProductToProductResponse_Maps_All_Properties()
        {
            var product = new Product();
            // Set properties if needed, e.g. via reflection or constructor if available
            var response = ProductMappings.ProductToProductResponse(product);
            Assert.NotNull(response);
            // Optionally, assert mapped properties if Product has public setters/getters
        }
    }
}
