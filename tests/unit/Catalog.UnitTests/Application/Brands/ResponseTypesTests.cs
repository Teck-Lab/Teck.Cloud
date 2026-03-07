using Catalog.Application.Brands.Features.CreateBrand.V1;
using Catalog.Application.Brands.Features.GetPaginatedBrands.V1;
using Catalog.Application.Brands.Features.UpdateBrand.V1;

namespace Catalog.UnitTests.Application.Brands
{
    public class ResponseTypesTests
    {
        [Fact]
        public void CreateBrandResponseDefaultsAreCorrect()
        {
            var resp = new CreateBrandResponse();
            Assert.Equal(Guid.Empty, resp.Id);
            Assert.Equal(string.Empty, resp.Name);
            Assert.Null(resp.Description);
            Assert.Null(resp.LogoUrl);
            Assert.Null(resp.WebsiteUrl);
        }

        [Fact]
        public void UpdateBrandResponseCanSetAllProperties()
        {
            var id = Guid.NewGuid();
            var websiteUri = new Uri("https://brand.com");
            var resp = new UpdateBrandResponse
            {
                Id = id,
                Name = "Brand",
                Description = "desc",
                LogoUrl = new Uri("https://brand.com/logo.png"),
                WebsiteUrl = websiteUri
            };
            Assert.Equal(id, resp.Id);
            Assert.Equal("Brand", resp.Name);
            Assert.Equal("desc", resp.Description);
            Assert.NotNull(resp.LogoUrl);
            Assert.Equal(websiteUri, resp.WebsiteUrl);
        }

        [Fact]
        public void GetPaginatedBrandsResponseAllowsNulls()
        {
            var resp = new GetPaginatedBrandsResponse
            {
                Description = null,
                LogoUrl = null,
                WebsiteUrl = null
            };
            Assert.Null(resp.Description);
            Assert.Null(resp.LogoUrl);
            Assert.Null(resp.WebsiteUrl);
        }
    }
}
