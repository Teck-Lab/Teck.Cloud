using Catalog.Application.Brands.Features.DeleteBrands.V1;
using Shouldly;

namespace Catalog.UnitTests.Application.Brands;

public class DeleteBrandsValidatorTests
{
    private readonly DeleteBrandsValidator _validator;

    public DeleteBrandsValidatorTests()
    {
        _validator = new DeleteBrandsValidator();
    }

    [Fact]
    public async Task Validate_ShouldPass_WhenIdsAreValid()
    {
        var request = new DeleteBrandsRequest
        {
            Ids = [Guid.NewGuid()]
        };

        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenIdsAreEmpty()
    {
        var request = new DeleteBrandsRequest
        {
            Ids = []
        };

        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Ids");
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Validate_ShouldFail_WhenIdsContainDefaultGuid(string guidString)
    {
        var request = new DeleteBrandsRequest
        {
            Ids = [Guid.Parse(guidString)]
        };

        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.StartsWith("Ids", StringComparison.Ordinal));
    }
}
