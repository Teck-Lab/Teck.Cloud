using Catalog.Application.Features.Products.GetProductById.V1;
using Catalog.Application.Products.Features.CreateProduct.V1;

namespace Catalog.UnitTests.Application.Products
{
    public class RequestTypesTests
    {
        [Fact]
        public void CreateProductRequest_CanSetProperties()
        {
            var req = new CreateProductRequest
            {
                Name = "Test Product",
                Description = "desc",
                ProductSku = "sku",
                GTIN = "gtin",
                IsActive = true,
                BrandId = Guid.NewGuid(),
                CategoryIds = new List<Guid> { Guid.NewGuid() }
            };
            
            Assert.Equal("Test Product", req.Name);
            Assert.Equal("desc", req.Description);
            Assert.Equal("sku", req.ProductSku);
            Assert.Equal("gtin", req.GTIN);
            Assert.True(req.IsActive);
            Assert.NotNull(req.BrandId);
            Assert.Single(req.CategoryIds);
        }

        [Fact]
        public void GetProductByIdRequest_CanSetProductId()
        {
            var id = Guid.NewGuid();
            var req = new GetProductByIdRequest { ProductId = id };
            Assert.Equal(id, req.ProductId);
        }

        [Fact]
        public void CreateProductRequest_Default_Properties()
        {
            var req = new CreateProductRequest();
            Assert.Equal(string.Empty, req.Name);
            Assert.Null(req.Description);
            Assert.Equal(string.Empty, req.ProductSku);
            Assert.Null(req.GTIN);
            Assert.True(req.IsActive);
            Assert.Null(req.BrandId);
            Assert.Empty(req.CategoryIds);
        }

        [Fact]
        public void GetProductByIdRequest_Default_Properties()
        {
            var req = new GetProductByIdRequest();
            Assert.Equal(Guid.Empty, req.ProductId);
        }
    }
}
