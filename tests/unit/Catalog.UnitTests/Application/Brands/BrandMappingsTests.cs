using Catalog.Application.Brands.Mappings;
using Catalog.Domain.Entities.BrandAggregate;

namespace Catalog.UnitTests.Application.Brands
{
    public class BrandMappingsTests
    {
        [Fact]
        public void BrandToCreateBrandResponse_Maps_All_Properties()
        {
            var brandResult = Brand.Create("Brand", "Description", "https://brand.com");
            Assert.False(brandResult.IsError);

            var response = BrandMapper.BrandToCreateBrandResponse(brandResult.Value);

            Assert.NotNull(response);
            Assert.Equal("Brand", response.Name);
        }

        [Fact]
        public void BrandToUpdateBrandResponse_Maps_All_Properties()
        {
            var brandResult = Brand.Create("Brand", "Description", "https://brand.com");
            Assert.False(brandResult.IsError);

            var response = BrandMapper.BrandToUpdateBrandResponse(brandResult.Value);

            Assert.NotNull(response);
            Assert.Equal("Brand", response.Name);
        }
    }
}
