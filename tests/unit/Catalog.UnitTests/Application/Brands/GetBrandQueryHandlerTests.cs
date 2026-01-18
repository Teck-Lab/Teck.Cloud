using Catalog.Application.Brands.Features.GetBrandById.V1;
using Catalog.Application.Brands.Repositories;
using Catalog.Application.Brands.ReadModels;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Application.Brands
{
    public class GetBrandQueryHandlerTests
    {
        [Fact]
        public async Task Handle_Should_ReturnSuccessResult_WhenBrandIsNotNull_Async()
        {
            // Arrange
            var cache = Substitute.For<IBrandCache>();
            var brandId = Guid.NewGuid();
            var brandReadModel = new BrandReadModel 
            { 
                Id = brandId, 
                Name = "Test Brand", 
                Description = "Test Description" 
            };

            cache.GetOrSetByIdAsync(brandId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(brandReadModel);

            var sut = new GetBrandByIdQueryHandler(cache);
            var request = new GetBrandByIdQuery(brandId);

            // Act
            var result = await sut.Handle(request, CancellationToken.None);

            // Assert
            result.IsError.ShouldBeFalse();
            result.Value.ShouldNotBeNull();
            result.Value.Id.ShouldBe(brandId);
        }

        [Fact]
        public async Task Handle_Should_ReturnNotFoundResult_WhenBrandIsNull_Async()
        {
            // Arrange
            var cache = Substitute.For<IBrandCache>();
            var brandId = Guid.NewGuid();

            cache.GetOrSetByIdAsync(brandId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns((BrandReadModel?)null);

            var sut = new GetBrandByIdQueryHandler(cache);
            var request = new GetBrandByIdQuery(brandId);

            // Act
            var result = await sut.Handle(request, CancellationToken.None);

            // Assert
            result.IsError.ShouldBeTrue();
        }
    }
}
