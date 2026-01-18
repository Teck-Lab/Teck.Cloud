using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Catalog.Domain.Entities.ProductPriceTypeAggregate;
using Catalog.Domain.Entities.ProductPriceTypeAggregate.Errors;
using Shouldly;

namespace Catalog.UnitTests.Domain.ProductPriceTypes;

public class ProductPriceTypeTests
{
    private readonly IFixture _fixture;

    public ProductPriceTypeTests()
    {
        _fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
    }

    [Fact]
    public void Create_Should_SetCorrectProperties()
    {
        // Arrange
        var name = _fixture.Create<string>();
        var priority = _fixture.Create<int>();

        // Act
        var result = ProductPriceType.Create(name, priority);

        // Assert
        result.IsError.ShouldBeFalse();
        var priceType = result.Value;
        priceType.Name.ShouldBe(name);
        priceType.Priority.ShouldBe(priority);
        priceType.ProductPrices.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_Should_ReturnError_When_NameIsInvalid(string? invalidName)
    {
        // Arrange
        var priority = _fixture.Create<int>();

        // Act
        var result = ProductPriceType.Create(invalidName!, priority);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("name");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_Should_ReturnError_When_PriorityIsNegative(int invalidPriority)
    {
        // Arrange
        var name = _fixture.Create<string>();

        // Act
        var result = ProductPriceType.Create(name, invalidPriority);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("cannot be negative");
    }

    [Fact]
    public void Create_Should_Succeed_When_Valid()
    {
        // Arrange
        var name = _fixture.Create<string>();
        var priority = 1;

        // Act
        var result = ProductPriceType.Create(name, priority);

        // Assert
        result.IsError.ShouldBeFalse();
        var ppt = result.Value;
        ppt.Name.ShouldBe(name);
        ppt.Priority.ShouldBe(priority);
    }

    [Fact]
    public void Create_Should_ReturnMultipleErrors_When_MultipleFieldsInvalid()
    {
        // Arrange
        string? name = null;
        int priority = -1;

        // Act
        var result = ProductPriceType.Create(name!, priority);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Update_Should_ModifyProperties()
    {
        // Arrange
        var createResult = ProductPriceType.Create(
            _fixture.Create<string>(),
            1
        );
        var priceType = createResult.Value;
        var newName = _fixture.Create<string>();
        var newPriority = 2;

        // Act
        var updateResult = priceType.Update(newName, newPriority);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        priceType.Name.ShouldBe(newName);
        priceType.Priority.ShouldBe(newPriority);
    }

    [Fact]
    public void Update_Should_Succeed_When_AllFieldsNull()
    {
        // Arrange
        var createResult = ProductPriceType.Create(_fixture.Create<string>(), 1);
        var ppt = createResult.Value;
        var originalName = ppt.Name;
        var originalPriority = ppt.Priority;

        // Act
        var updateResult = ppt.Update(null, null);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        ppt.Name.ShouldBe(originalName);
        ppt.Priority.ShouldBe(originalPriority);
    }

    [Fact]
    public void Update_Should_ReturnError_When_NameIsEmpty()
    {
        // Arrange
        var createResult = ProductPriceType.Create(_fixture.Create<string>(), 1);
        var ppt = createResult.Value;

        // Act
        var updateResult = ppt.Update("", null);

        // Assert
        updateResult.IsError.ShouldBeTrue();
        updateResult.FirstError.Description.ShouldContain("name");
    }

    [Fact]
    public void Update_Should_ReturnError_When_PriorityIsNegative()
    {
        // Arrange
        var createResult = ProductPriceType.Create(_fixture.Create<string>(), 1);
        var ppt = createResult.Value;

        // Act
        var updateResult = ppt.Update(null, -1);

        // Assert
        updateResult.IsError.ShouldBeTrue();
        updateResult.FirstError.Description.ShouldContain("negative");
    }

    [Fact]
    public void Update_Should_NotChange_When_ValuesAreSame()
    {
        // Arrange
        var name = _fixture.Create<string>();
        var priority = 1;
        var createResult = ProductPriceType.Create(name, priority);
        var ppt = createResult.Value;

        // Act
        var updateResult = ppt.Update(name, priority);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        ppt.Name.ShouldBe(name);
        ppt.Priority.ShouldBe(priority);
    }

    [Fact]
    public void ProductPriceTypeErrors_Should_Have_Correct_Values()
    {
        var notFound = ProductPriceTypeErrors.NotFound;
        notFound.Code.ShouldBe("ProductPriceType.NotFound");
        notFound.Description.ShouldContain("not found");

        var notCreated = ProductPriceTypeErrors.NotCreated;
        notCreated.Code.ShouldBe("ProductPriceType.NotCreated");
        notCreated.Description.ShouldContain("not created");

        var emptyName = ProductPriceTypeErrors.EmptyName;
        emptyName.Code.ShouldBe("ProductPriceType.EmptyName");
        emptyName.Description.ShouldContain("cannot be empty");

        var negativePriority = ProductPriceTypeErrors.NegativePriority;
        negativePriority.Code.ShouldBe("ProductPriceType.NegativePriority");
        negativePriority.Description.ShouldContain("negative");
    }
}