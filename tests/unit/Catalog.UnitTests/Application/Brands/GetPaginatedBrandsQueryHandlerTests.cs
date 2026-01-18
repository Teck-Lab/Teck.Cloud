using SharedKernel.Core.Pagination;
using Catalog.Application.Brands.Features.Responses;
using Catalog.Application.Brands.Features.GetPaginatedBrands.V1;
using Catalog.Application.Brands.Repositories;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Application.Brands
{
    public class GetPaginatedBrandsQueryHandlerTests
    {
        [Fact]
        public async Task Handle_Should_ReturnEmptyBrandResponseList_WhenBrandsIsFound_Async()
        {
            // Arrange
            var brandRepository = Substitute.For<IBrandReadRepository>();
            var expected = new PagedList<BrandResponse>([], 0, 1, 10);
            var request = new GetPaginatedBrandsQuery(1, 10, "test");

            var sut = new GetPaginatedBrandsQueryHandler(brandRepository);

            // Act
            var result = await sut.Handle(request, CancellationToken.None);

            // Assert
            result.IsError.ShouldBe(false);
            result.Value.TotalItems.ShouldBe(expected.TotalItems);
        }
    }
}
