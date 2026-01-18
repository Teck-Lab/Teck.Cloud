using AutoFixture;
using AutoFixture.AutoNSubstitute;
using SharedKernel.Core.Database;
using Catalog.Application.Brands.Features.DeleteBrand.V1;
using Catalog.Application.Brands.Repositories;
using Catalog.Domain.Entities.BrandAggregate;
using Catalog.Domain.Entities.BrandAggregate.Repositories;
using ErrorOr;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Shouldly;
using Soenneker.Utils.AutoBogus;
using Soenneker.Utils.AutoBogus.Config;

namespace Catalog.UnitTests.Application.Brands
{
    public class DeleteBrandCommandHandlerTests
    {
        //private readonly Substitute<IBrandRepository> _brandRepositoryMock;

        [Fact]
        public async Task Handle_Should_ReturnDeletedResult_WhenBrandIsDeleted_Async()
        {
            //Arrange
            IFixture fixture = new Fixture().Customize(new AutoNSubstituteCustomization() { ConfigureMembers = true });

            DeleteBrandCommandHandler sut = fixture.Create<DeleteBrandCommandHandler>();

            var optionalConfig = new AutoFakerConfig();
            var autoFaker = new AutoFaker(optionalConfig);

            var command = autoFaker.Generate<DeleteBrandCommand>();

            //Act
            ErrorOr<Deleted> result = await sut.Handle(command, TestContext.Current.CancellationToken);

            //Assert
            result.IsError.ShouldBeFalse();
        }
        [Fact]
        public async Task Handle_Should_ReturnNotFoundResult_WhenBrandIsNotFound_Async()
        {
            //Arrange
            IFixture fixture = new Fixture().Customize(new AutoNSubstituteCustomization() { ConfigureMembers = true });

            var optionalConfig = new AutoFakerConfig();
            var autoFaker = new AutoFaker(optionalConfig);

            var command = autoFaker.Generate<DeleteBrandCommand>();

            var repo = fixture.Freeze<IBrandWriteRepository>();
            repo.FirstOrDefaultAsync(Arg.Any<Catalog.Domain.Entities.BrandAggregate.Specifications.BrandByIdSpecification>(), Arg.Any<CancellationToken>()).ReturnsNullForAnyArgs();

            DeleteBrandCommandHandler sut = fixture.Create<DeleteBrandCommandHandler>();

            //Act
            ErrorOr<Deleted> result = await sut.Handle(command, TestContext.Current.CancellationToken);

            //Assert
            result.IsError.ShouldBeTrue();
        }

        [Fact]
        public async Task Handle_Should_Throw_WhenRepositoryThrows_Async()
        {
            var repo = Substitute.For<IBrandWriteRepository>();
            var uow = Substitute.For<IUnitOfWork>();
            var cache = Substitute.For<IBrandCache>();
            repo.FirstOrDefaultAsync(Arg.Any<Catalog.Domain.Entities.BrandAggregate.Specifications.BrandByIdSpecification>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<Brand?>(new Exception("repo error")));
            var sut = new DeleteBrandCommandHandler(uow, cache, repo);
            var command = new DeleteBrandCommand(Guid.NewGuid());
            await Should.ThrowAsync<Exception>(async () => await sut.Handle(command, default));
        }

        [Fact]
        public async Task Handle_Should_Throw_WhenUnitOfWorkThrows_Async()
        {
            var repo = Substitute.For<IBrandWriteRepository>();
            var uow = Substitute.For<IUnitOfWork>();
            var cache = Substitute.For<IBrandCache>();
            repo.FirstOrDefaultAsync(Arg.Any<Catalog.Domain.Entities.BrandAggregate.Specifications.BrandByIdSpecification>(), Arg.Any<CancellationToken>()).Returns(new Brand());
            uow.When(x => x.SaveChangesAsync(Arg.Any<CancellationToken>())).Do(x => throw new Exception("uow error"));
            var sut = new DeleteBrandCommandHandler(uow, cache, repo);
            var command = new DeleteBrandCommand(Guid.NewGuid());
            await Should.ThrowAsync<Exception>(async () => await sut.Handle(command, default));
        }

        [Fact]
        public async Task Handle_Should_Throw_WhenCacheThrows_Async()
        {
            var repo = Substitute.For<IBrandWriteRepository>();
            var uow = Substitute.For<IUnitOfWork>();
            var cache = Substitute.For<IBrandCache>();
            repo.FirstOrDefaultAsync(Arg.Any<Catalog.Domain.Entities.BrandAggregate.Specifications.BrandByIdSpecification>(), Arg.Any<CancellationToken>()).Returns(new Brand());
            uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(1));
            cache.When(x => x.RemoveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())).Do(x => throw new Exception("cache error"));
            var sut = new DeleteBrandCommandHandler(uow, cache, repo);
            var command = new DeleteBrandCommand(Guid.NewGuid());
            await Should.ThrowAsync<Exception>(async () => await sut.Handle(command, default));
        }
    }
}
