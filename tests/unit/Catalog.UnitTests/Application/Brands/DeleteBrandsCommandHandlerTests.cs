using Catalog.Application.Brands.Features.DeleteBrands.V1;
using Catalog.Application.Brands.Repositories;
using Catalog.Domain.Entities.BrandAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Application.Brands
{
    public class DeleteBrandsCommandHandlerTests
    {
        [Fact]
        public async Task Handle_Should_ReturnDeletedResult_WhenBrandsIsDeleted_Async()
        {
            // Arrange
            var brandRepo = Substitute.For<IBrandWriteRepository>();
            var cache = Substitute.For<IBrandCache>();
            var brandIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

            var sut = new DeleteBrandsCommandHandler(cache, brandRepo);
            var request = new DeleteBrandsCommand(brandIds);

            brandRepo.ExcecutSoftDeleteAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            // Act
            var result = await sut.Handle(request, CancellationToken.None);

            // Assert
            result.IsError.ShouldBeFalse();
            result.Value.ShouldBe(Result.Deleted);
        }
    }
}
