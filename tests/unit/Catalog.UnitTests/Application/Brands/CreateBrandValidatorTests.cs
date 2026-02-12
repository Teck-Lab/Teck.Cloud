using Catalog.Application.Brands.Features.CreateBrand.V1;
using Catalog.Application.Brands.Repositories;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Application.Brands;

public class CreateBrandValidatorTests
{
    private readonly IBrandReadRepository _brandReadRepository;
    private readonly CreateBrandValidator _validator;

    public CreateBrandValidatorTests()
    {
        _brandReadRepository = Substitute.For<IBrandReadRepository>();
        _validator = new CreateBrandValidator(_brandReadRepository);
    }

    [Fact]
    public async Task Validate_ShouldPass_WhenNameIsValidAndUnique()
    {
        var request = new CreateBrandRequest { Name = "GoodBrand" };
        _brandReadRepository.ExistsAsync(
            Arg.Any<System.Linq.Expressions.Expression<System.Func<Catalog.Application.Brands.ReadModels.BrandReadModel, bool>>>(),
            false,
            Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(false));
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new CreateBrandRequest { Name = "" };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNameExceedsMaxLength()
    {
        var request = new CreateBrandRequest { Name = new string('A', 101) };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNameIsDuplicate()
    {
        var request = new CreateBrandRequest { Name = "ExistingBrand" };
        _brandReadRepository.ExistsAsync(
            Arg.Any<System.Linq.Expressions.Expression<System.Func<Catalog.Application.Brands.ReadModels.BrandReadModel, bool>>>(),
            false,
            Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(true));
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("Brand with the name 'ExistingBrand' already Exists"));
    }

    [Theory]
    [InlineData("汉字品牌")] // Chinese
    [InlineData("Brand💼")] // Emoji
    [InlineData("Brand123")]
    [InlineData("A")]
    public async Task Validate_ShouldPass_ForVariousValidNames(string validName)
    {
        var request = new CreateBrandRequest { Name = validName };
        _brandReadRepository.ExistsAsync(
            Arg.Any<System.Linq.Expressions.Expression<System.Func<Catalog.Application.Brands.ReadModels.BrandReadModel, bool>>>(),
            false,
            Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(false));
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeTrue();
    }
}
