using Catalog.Application.Brands.Features.DeleteBrand.V1;
using Shouldly;

namespace Catalog.UnitTests.Application.Brands;

public class DeleteBrandValidatorTests
{
    private readonly DeleteBrandValidator _validator;

    public DeleteBrandValidatorTests()
    {
        _validator = new DeleteBrandValidator();
    }

    [Fact]
    public async Task Validate_ShouldPass_WhenIdIsValid()
    {
        var request = new DeleteBrandRequest
        {
            Id = Guid.NewGuid()
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenIdIsEmpty()
    {
        var request = new DeleteBrandRequest
        {
            Id = Guid.Empty
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Id");
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Validate_ShouldFail_WhenIdIsDefaultGuid(string guidString)
    {
        var request = new DeleteBrandRequest
        {
            Id = Guid.Parse(guidString)
        };
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Id");
    }
}
