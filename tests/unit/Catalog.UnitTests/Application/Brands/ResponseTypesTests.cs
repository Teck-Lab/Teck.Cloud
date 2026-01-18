using Catalog.Application.Brands.Features.Responses;

namespace Catalog.UnitTests.Application.Brands
{
    public class ResponseTypesTests
    {
        [Fact]
        public void BrandResponseDefaultsAreCorrect()
        {
            var resp = new BrandResponse();
            Assert.Equal(Guid.Empty, resp.Id);
            Assert.Equal(string.Empty, resp.Name);
            Assert.Null(resp.Description);
            Assert.Null(resp.WebsiteUrl);
        }

        [Fact]
        public void BrandResponseCanSetAllProperties()
        {
            var id = Guid.NewGuid();
            var websiteUri = new Uri("https://brand.com");
            var resp = new BrandResponse
            {
                Id = id,
                Name = "Brand",
                Description = "desc",
                WebsiteUrl = websiteUri
            };
            Assert.Equal(id, resp.Id);
            Assert.Equal("Brand", resp.Name);
            Assert.Equal("desc", resp.Description);
            Assert.Equal(websiteUri, resp.WebsiteUrl);
        }

        [Fact]
        public void BrandResponseAllowsNulls()
        {
            var resp = new BrandResponse
            {
                Description = null,
                WebsiteUrl = null
            };
            Assert.Null(resp.Description);
            Assert.Null(resp.WebsiteUrl);
        }
    }
}
