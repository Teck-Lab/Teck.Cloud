using Catalog.Application.Features.ProductPrices.Response;

namespace Catalog.UnitTests.Application.ProductPrices
{
    public class ResponseTypesTests
    {
        [Fact]
        public void ProductPriceResponse_Defaults_Are_Correct()
        {
            var resp = new ProductPriceResponse();
            Assert.Equal(0m, resp.SalePrice);
            Assert.Null(resp.CurrencyCode);
        }

        [Fact]
        public void ProductPriceResponse_CanSet_All_Properties()
        {
            var resp = new ProductPriceResponse
            {
                SalePrice = 9.99m,
                CurrencyCode = "USD"
            };
            Assert.Equal(9.99m, resp.SalePrice);
            Assert.Equal("USD", resp.CurrencyCode);
        }

        [Fact]
        public void ProductPriceResponse_Allows_Nulls()
        {
            var resp = new ProductPriceResponse
            {
                CurrencyCode = null
            };
            Assert.Null(resp.CurrencyCode);
        }
    }
}
