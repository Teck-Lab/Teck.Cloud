using Catalog.Application.Categories.Features.GetCategoryById.V1;
using Shouldly;

namespace Catalog.UnitTests.Application.Categories;

public class GetCategoryByIdValidatorTests
{
    private readonly GetCategoryByIdValidator _validator;

    public GetCategoryByIdValidatorTests()
    {
        _validator = new GetCategoryByIdValidator();
    }

    [Fact]
    public void Validate_ShouldPass_WhenIdIsValid()
    {
        // Arrange
        var request = new GetCategoryByIdRequest(Guid.NewGuid());

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenIdIsEmpty()
    {
        // Arrange
        var request = new GetCategoryByIdRequest(Guid.Empty);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].PropertyName.ShouldBe("Id");
    }
}
