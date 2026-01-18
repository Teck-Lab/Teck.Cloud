
using Catalog.Application.Brands.Mappings;
using Catalog.Domain.Entities.BrandAggregate;
using SharedKernel.Core.Pagination;

namespace Catalog.UnitTests.Application.Brands
{
    public class BrandMappingsTests
    {
        [Fact]
        public void BrandToBrandResponse_Maps_All_Properties()
        {
            var brand = new Brand();
            var response = BrandMapper.BrandToBrandResponse(brand);
            Assert.NotNull(response);
        }

        [Fact]
        public void PagedBrandToPagedBrandResponse_Maps_All_Properties()
        {
            var brands = new PagedList<Brand>(new List<Brand> { new Brand() }, 1, 1, 1);
            var pagedResponse = BrandMapper.PagedBrandToPagedBrandResponse(brands);
            Assert.NotNull(pagedResponse);
            Assert.Single(pagedResponse.Items);
        }
    }
}
