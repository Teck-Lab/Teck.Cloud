using Catalog.Application.Brands.Features.UpdateBrand.V1;
using Shouldly;

namespace Catalog.UnitTests.Application.Brands;

public class UpdateBrandValidatorTests
{
    private readonly UpdateBrandValidator _validator;

    public UpdateBrandValidatorTests()
    {
        _validator = new UpdateBrandValidator();
    }

    [Fact]
    public async Task Validate_ShouldPass_WhenIdAndNameAreValid()
    {
        var request = new UpdateBrandRequest
        {
            Id = Guid.NewGuid(),
            Name = "ValidBrandName"
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenIdIsEmpty()
    {
        var request = new UpdateBrandRequest
        {
            Id = Guid.Empty,
            Name = "ValidBrandName"
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Id");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new UpdateBrandRequest
        {
            Id = Guid.NewGuid(),
            Name = ""
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNameIsNull()
    {
        var request = new UpdateBrandRequest
        {
            Id = Guid.NewGuid(),
            Name = null
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNameExceedsMaxLength()
    {
        var request = new UpdateBrandRequest
        {
            Id = Guid.NewGuid(),
            Name = new string('A', 101)
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Theory]
    [InlineData("A")] // Minimum valid length
    [InlineData("汉字品牌")] // Chinese
    [InlineData("Brand💼")] // Emoji
    [InlineData("Brand123")]
    [InlineData(100)] // Exactly max length
    public async Task Validate_ShouldPass_ForVariousValidNames(object name)
    {
        var request = new UpdateBrandRequest
        {
            Id = Guid.NewGuid(),
            Name = name is int length ? new string('A', length) : name?.ToString()
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenBothIdAndNameAreInvalid()
    {
        var request = new UpdateBrandRequest
        {
            Id = Guid.Empty,
            Name = ""
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(2);
        result.Errors.ShouldContain(e => e.PropertyName == "Id");
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }
}
