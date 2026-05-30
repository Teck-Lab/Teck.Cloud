using Billing.Application.Billing.Features.GetPaginatedBillingTransactions.V1;
using Shouldly;

namespace Billing.UnitTests.Application.Queries;

public sealed class GetPaginatedBillingTransactionsValidatorTests
{
    private readonly GetPaginatedBillingTransactionsValidator validator = new();

    [Fact]
    public async Task Validate_WhenPageAndSizeAreWithinBounds_ShouldSucceed()
    {
        // Arrange
        GetPaginatedBillingTransactionsRequest request = new()
        {
            Page = 1,
            Size = 100,
            TenantId = Guid.NewGuid(),
            Status = "Succeeded",
        };

        // Act
        var result = await this.validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_WhenPageIsLessThanOne_ShouldFail(int page)
    {
        // Arrange
        GetPaginatedBillingTransactionsRequest request = new()
        {
            Page = page,
            Size = 10,
        };

        // Act
        var result = await this.validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Validate_WhenSizeIsOutsideAllowedRange_ShouldFail(int size)
    {
        // Arrange
        GetPaginatedBillingTransactionsRequest request = new()
        {
            Page = 1,
            Size = size,
        };

        // Act
        var result = await this.validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_WhenSizeExceedsMaxPageSize_ShouldClampAndSucceed()
    {
        // Arrange
        GetPaginatedBillingTransactionsRequest request = new()
        {
            Page = 1,
            Size = 101,
        };

        // Act
        var result = await this.validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
        request.Size.ShouldBe(100);
    }

    [Fact]
    public async Task Validate_WhenOptionalFiltersAreNotProvided_ShouldSucceed()
    {
        // Arrange
        GetPaginatedBillingTransactionsRequest request = new()
        {
            Page = 2,
            Size = 25,
            TenantId = null,
            Status = null,
        };

        // Act
        var result = await this.validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
