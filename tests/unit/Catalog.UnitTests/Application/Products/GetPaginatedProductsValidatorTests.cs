using Catalog.Application.Products.Features.GetPaginatedProducts.V1;
using Shouldly;

namespace Catalog.UnitTests.Application.Products;

public sealed class GetPaginatedProductsValidatorTests
{
    private readonly GetPaginatedProductsValidator validator = new();

    [Fact]
    public async Task Validate_ShouldPass_WhenPageAndSizeAreValid()
    {
        GetPaginatedProductsRequest request = new()
        {
            Page = 1,
            Size = 10,
            Keyword = "laptop",
        };

        var result = await this.validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public async Task Validate_ShouldFail_WhenPageOrSizeInvalid(int page, int size)
    {
        GetPaginatedProductsRequest request = new()
        {
            Page = page,
            Size = size,
        };

        var result = await this.validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }
}
