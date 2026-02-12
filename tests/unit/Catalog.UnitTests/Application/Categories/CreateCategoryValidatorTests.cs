using Catalog.Application.Categories.Features.CreateCategory.V1;
using Catalog.Application.Categories.Repositories;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Application.Categories;

public class CreateCategoryValidatorTests
{
    private readonly ICategoryReadRepository _categoryReadRepository;
    private readonly CreateCategoryValidator _validator;

    public CreateCategoryValidatorTests()
    {
        _categoryReadRepository = Substitute.For<ICategoryReadRepository>();
        _validator = new CreateCategoryValidator(_categoryReadRepository);
    }

    [Fact]
    public async Task Validate_ShouldPass_WhenNameIsValidAndUnique()
    {
        var request = new CreateCategoryRequest("GoodCategory", null);
        _categoryReadRepository.ExistsAsync(
            Arg.Any<System.Linq.Expressions.Expression<System.Func<Catalog.Application.Categories.ReadModels.CategoryReadModel, bool>>>(),
            false,
            Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(false));
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new CreateCategoryRequest("", null);
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNameExceedsMaxLength()
    {
        var request = new CreateCategoryRequest(new string('A', 101), null);
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNameIsDuplicate()
    {
        var request = new CreateCategoryRequest("ExistingCategory", null);
        _categoryReadRepository.ExistsAsync(
            Arg.Any<System.Linq.Expressions.Expression<System.Func<Catalog.Application.Categories.ReadModels.CategoryReadModel, bool>>>(),
            false,
            Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(true));
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("Category with the name 'ExistingCategory' already Exists"));
    }

    [Theory]
    [InlineData("汉字分类")] // Chinese
    [InlineData("Category💼")] // Emoji
    [InlineData("Category123")]
    [InlineData("A")]
    public async Task Validate_ShouldPass_ForVariousValidNames(string validName)
    {
        var request = new CreateCategoryRequest(validName, null);
        _categoryReadRepository.ExistsAsync(
            Arg.Any<System.Linq.Expressions.Expression<System.Func<Catalog.Application.Categories.ReadModels.CategoryReadModel, bool>>>(),
            false,
            Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(false));
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(100)] // Exactly max length
    [InlineData(99)]  // One less than max
    public async Task Validate_ShouldPass_WhenNameAtMaxLength(int length)
    {
        var request = new CreateCategoryRequest(new string('A', length), null);
        _categoryReadRepository.ExistsAsync(
            Arg.Any<System.Linq.Expressions.Expression<System.Func<Catalog.Application.Categories.ReadModels.CategoryReadModel, bool>>>(),
            false,
            Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.FromResult(false));
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeTrue();
    }
}
