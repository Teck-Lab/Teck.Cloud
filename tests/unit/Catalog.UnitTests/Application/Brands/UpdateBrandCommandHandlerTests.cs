using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Catalog.Application.Brands.Features.Responses;
using Catalog.Application.Brands.Features.UpdateBrand.V1;
using Catalog.Domain.Entities.BrandAggregate.Repositories;
using Catalog.Domain.Entities.BrandAggregate.Specifications;
using ErrorOr;
using NSubstitute.ReturnsExtensions;
using Shouldly;
using Soenneker.Utils.AutoBogus;
using Soenneker.Utils.AutoBogus.Config;
using Soenneker.Utils.AutoBogus.Context;
using Soenneker.Utils.AutoBogus.Override;

namespace Catalog.UnitTests.Application.Brands
{
    public class UpdateBrandCommandHandlerTests
    {
        //private readonly Substitute<IBrandRepository> _brandRepositoryMock;

        [Fact]
        public async Task Handle_Should_ReturnSuccessResult_WhenBrandIsUpdated_Async()
        {
            //Arrange
            IFixture fixture = new Fixture().Customize(new AutoNSubstituteCustomization() { ConfigureMembers = true });

            UpdateBrandCommandHandler sut = fixture.Create<UpdateBrandCommandHandler>();

            var optionalConfig = new AutoFakerConfig();
            var autoFaker = new AutoFaker(optionalConfig);
            autoFaker.Config.Overrides = [new UpdateBrandRequestOverride()];

            var request = autoFaker.Generate<UpdateBrandRequest>();

            UpdateBrandCommand command = new(request.Id, request.Name, request.Description, request.Website);

            //Act
            ErrorOr<BrandResponse> result = await sut.Handle(command, TestContext.Current.CancellationToken);

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
            autoFaker.Config.Overrides = [new UpdateBrandRequestOverride()];


            var expected = autoFaker.Generate<BrandResponse>();

            var brandByIdSpec = new BrandByIdSpecification(expected.Id);

            fixture.Freeze<IBrandWriteRepository>().FirstOrDefaultAsync(brandByIdSpec, TestContext.Current.CancellationToken).ReturnsNullForAnyArgs();

            UpdateBrandCommandHandler sut = fixture.Create<UpdateBrandCommandHandler>();

            var request = autoFaker.Generate<UpdateBrandRequest>();

            UpdateBrandCommand command = new(request.Id, request.Name, request.Description, request.Website);

            //Act
            ErrorOr<BrandResponse> result = await sut.Handle(command, TestContext.Current.CancellationToken);

            //Assert
            result.IsError.ShouldBeTrue();
        }
    }

    internal class UpdateBrandRequestOverride : AutoFakerOverride<UpdateBrandRequest>
    {
        public override void Generate(AutoFakerOverrideContext context)
        {
            var target = (context.Instance as UpdateBrandRequest)!;

            target.Name = context.Faker.Company.CompanyName();
            target.Description = context.Faker.Company.CatchPhrase();
            target.Website = context.Faker.Internet.Url();
        }
    }
}
