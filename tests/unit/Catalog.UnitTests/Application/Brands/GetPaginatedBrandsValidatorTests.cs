using Catalog.Application.Brands.Features.GetPaginatedBrands.V1;
using Shouldly;

namespace Catalog.UnitTests.Application.Brands;

public class GetPaginatedBrandsValidatorTests
{
    private readonly GetPaginatedBrandsValidator _validator;

    public GetPaginatedBrandsValidatorTests()
    {
        _validator = new GetPaginatedBrandsValidator();
    }

    [Fact]
    public async Task Validate_ShouldPass_WhenPageAndSizeAreValid()
    {
        var request = new GetPaginatedBrandsRequest
        {
            Page = 1,
            Size = 10
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenPageIsZero()
    {
        var request = new GetPaginatedBrandsRequest
        {
            Page = 0,
            Size = 10
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Page");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenPageIsNegative()
    {
        var request = new GetPaginatedBrandsRequest
        {
            Page = -1,
            Size = 10
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Page");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenSizeIsZero()
    {
        var request = new GetPaginatedBrandsRequest
        {
            Page = 1,
            Size = 0
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Size");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenSizeIsNegative()
    {
        var request = new GetPaginatedBrandsRequest
        {
            Page = 1,
            Size = -5
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Size");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenBothPageAndSizeAreInvalid()
    {
        var request = new GetPaginatedBrandsRequest
        {
            Page = 0,
            Size = 0
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Page");
        result.Errors.ShouldContain(e => e.PropertyName == "Size");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(10, 10)]
    [InlineData(100, 50)]
    public async Task Validate_ShouldPass_ForVariousValidCombinations(int page, int size)
    {
        var request = new GetPaginatedBrandsRequest
        {
            Page = page,
            Size = size
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-5, 10)]
    [InlineData(10, -5)]
    [InlineData(-1, -1)]
    public async Task Validate_ShouldFail_ForVariousInvalidCombinations(int page, int size)
    {
        var request = new GetPaginatedBrandsRequest
        {
            Page = page,
            Size = size
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_ShouldPass_WithKeyword()
    {
        var request = new GetPaginatedBrandsRequest
        {
            Page = 1,
            Size = 10,
            Keyword = "search term"
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_ShouldPass_WithEmptyKeyword()
    {
        var request = new GetPaginatedBrandsRequest
        {
            Page = 1,
            Size = 10,
            Keyword = ""
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_ShouldPass_WithNullKeyword()
    {
        var request = new GetPaginatedBrandsRequest
        {
            Page = 1,
            Size = 10,
            Keyword = null
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeTrue();
    }
}
