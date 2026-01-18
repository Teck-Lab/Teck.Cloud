using Catalog.Application.Brands.Features.CreateBrand.V1;
using Catalog.Application.Brands.Features.Responses;
using Catalog.Domain.Entities.BrandAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Database;
using Shouldly;

namespace Catalog.UnitTests.Application.Brands
{
    public class CreateBrandCommandHandlerTests
    {
        [Fact]
        public async Task HandleShouldReturnSuccessResultWhenBrandIsUniqueAsync()
        {
            // Arrange
            var uow = Substitute.For<IUnitOfWork>();
            var repo = Substitute.For<IBrandWriteRepository>();

            uow.SaveChangesAsync(Arg.Any<CancellationToken>())
                .Returns(1);

            var sut = new CreateBrandCommandHandler(uow, repo);

            CreateBrandCommand command = new("Valid Brand Name", "Description", "https://example.com");

            // Act
            ErrorOr<BrandResponse> result = await sut.Handle(command, TestContext.Current.CancellationToken);

            // Assert
            result.IsError.ShouldBeFalse();
        }

        [Fact]
        public async Task HandleShouldReturnErrorWhenBrandIsInvalidAsync()
        {
            // Arrange
            var uow = Substitute.For<IUnitOfWork>();
            var repo = Substitute.For<IBrandWriteRepository>();
            var sut = new CreateBrandCommandHandler(uow, repo);

            CreateBrandCommand command = new(string.Empty, null, null);

            // Act
            ErrorOr<BrandResponse> result = await sut.Handle(command, TestContext.Current.CancellationToken);

            // Assert
            result.IsError.ShouldBeTrue();
        }
    }
}
