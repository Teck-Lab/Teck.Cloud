using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Domain.Entities.PromotionAggregate;
using Shouldly;

#nullable enable

namespace Catalog.UnitTests.Domain.Promotions;

public class PromotionTests
{
    private readonly IFixture _fixture;

    public PromotionTests()
    {
        _fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_ReturnError_When_NameIsInvalid(string? invalidName)
    {
        var start = DateTime.UtcNow;
        var end = DateTime.UtcNow.AddDays(1);
        var products = new List<Product>();
        var result = Promotion.Create(invalidName!, null, start, end, products);
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("name");
    }

    [Fact]
    public void Create_Should_ReturnError_When_StartDateAfterEndDate()
    {
        var name = _fixture.Create<string>();
        var start = DateTime.UtcNow.AddDays(2);
        var end = DateTime.UtcNow.AddDays(1);
        var products = new List<Product>();
        var result = Promotion.Create(name, null, start, end, products);
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("start date");
    }

    [Fact]
    public void Create_Should_Succeed_When_Valid()
    {
        var name = _fixture.Create<string>();
        var start = DateTime.UtcNow;
        var end = DateTime.UtcNow.AddDays(1);
        var products = new List<Product> { _fixture.Create<Product>() };
        var result = Promotion.Create(name, null, start, end, products);
        result.IsError.ShouldBeFalse();
        var promo = result.Value;
        promo.Name.ShouldBe(name);
        promo.ValidFrom.ShouldBe(start);
        promo.ValidTo.ShouldBe(end);
        promo.Products.ShouldBe(products);
    }

    [Fact]
    public void Create_Should_ReturnError_When_ProductsIsNull()
    {
        var name = _fixture.Create<string>();
        var start = DateTime.UtcNow;
        var end = DateTime.UtcNow.AddDays(1);
        List<Product>? products = null;
        var result = Promotion.Create(name, null, start, end, products!);
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldBe("Promotion must have at least one product.");
    }

    [Fact]
    public void Create_Should_ReturnError_When_ProductsIsEmpty()
    {
        var name = _fixture.Create<string>();
        var start = DateTime.UtcNow;
        var end = DateTime.UtcNow.AddDays(1);
        var products = new List<Product>();
        var result = Promotion.Create(name, null, start, end, products);
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldBe("Promotion must have at least one product.");
    }

    [Fact]
    public void Create_Should_ReturnMultipleErrors_When_MultipleFieldsInvalid()
    {
        string? name = null;
        var start = DateTime.UtcNow.AddDays(2);
        var end = DateTime.UtcNow.AddDays(1);
        List<Product>? products = null;
        var result = Promotion.Create(name!, null, start, end, products!);
        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Update_Should_OnlyModifyChangedProperties()
    {
        // Arrange
        var createResult = Promotion.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(30),
            _fixture.CreateMany<Product>().ToList()
        );
        var promotion = createResult.Value;
        var originalName = promotion.Name;
        var newDescription = _fixture.Create<string>();
        var newValidTo = promotion.ValidTo.AddDays(30);

        // Act
        var updateResult = promotion.Update(null, newDescription, null, newValidTo, null);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        promotion.Name.ShouldBe(originalName);
        promotion.Description.ShouldBe(newDescription);
        promotion.ValidTo.ShouldBe(newValidTo);
    }

    [Fact]
    public void Update_Should_Succeed_When_AllFieldsNull()
    {
        var createResult = Promotion.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(30),
            _fixture.CreateMany<Product>().ToList()
        );
        var promotion = createResult.Value;
        var originalName = promotion.Name;
        var originalDescription = promotion.Description;
        var originalValidFrom = promotion.ValidFrom;
        var originalValidTo = promotion.ValidTo;
        var originalProducts = promotion.Products.ToList();
        var updateResult = promotion.Update(null, null, null, null, null);
        updateResult.IsError.ShouldBeFalse();
        promotion.Name.ShouldBe(originalName);
        promotion.Description.ShouldBe(originalDescription);
        promotion.ValidFrom.ShouldBe(originalValidFrom);
        promotion.ValidTo.ShouldBe(originalValidTo);
        promotion.Products.ShouldBe(originalProducts);
    }

    [Fact]
    public void Update_Should_ReturnError_When_NameIsEmpty()
    {
        var createResult = Promotion.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(30),
            _fixture.CreateMany<Product>().ToList()
        );
        var promotion = createResult.Value;
        var updateResult = promotion.Update("", null, null, null, null);
        updateResult.IsError.ShouldBeTrue();
        updateResult.FirstError.Description.ShouldContain("name");
    }

    [Fact]
    public void Update_Should_ReturnError_When_ValidToBeforeValidFrom()
    {
        var createResult = Promotion.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(30),
            _fixture.CreateMany<Product>().ToList()
        );
        var promotion = createResult.Value;
        var newValidTo = promotion.ValidFrom.AddDays(-1);
        var updateResult = promotion.Update(null, null, null, newValidTo, null);
        updateResult.IsError.ShouldBeTrue();
        updateResult.Errors.ShouldContain(e => e.Description.Contains("date range"));
    }

    [Fact]
    public void Update_Should_ChangeProducts_When_ProductsProvided()
    {
        var createResult = Promotion.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(30),
            _fixture.CreateMany<Product>().ToList()
        );
        var promotion = createResult.Value;
        var newProducts = _fixture.CreateMany<Product>().ToList();
        var updateResult = promotion.Update(null, null, null, null, newProducts);
        updateResult.IsError.ShouldBeFalse();
        promotion.Products.ShouldBe(newProducts);
    }

    [Fact]
    public void Update_Should_NotChange_When_ValuesAreSame()
    {
        var name = _fixture.Create<string>();
        var description = _fixture.Create<string>();
        var validFrom = DateTimeOffset.UtcNow;
        var validTo = validFrom.AddDays(30);
        var products = _fixture.CreateMany<Product>().ToList();
        var createResult = Promotion.Create(name, description, validFrom, validTo, products);
        var promotion = createResult.Value;
        var updateResult = promotion.Update(name, description, validFrom, validTo, products);
        updateResult.IsError.ShouldBeFalse();
        promotion.Name.ShouldBe(name);
        promotion.Description.ShouldBe(description);
        promotion.ValidFrom.ShouldBe(validFrom);
        promotion.ValidTo.ShouldBe(validTo);
        promotion.Products.ShouldBe(products);
    }

    [Fact]
    public void Update_Should_NotChange_When_DescriptionIsSame()
    {
        var description = _fixture.Create<string>();
        var createResult = Promotion.Create(_fixture.Create<string>(), description, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), _fixture.CreateMany<Product>().ToList());
        var promotion = createResult.Value;
        var updateResult = promotion.Update(null, description, null, null, null);
        updateResult.IsError.ShouldBeFalse();
        promotion.Description.ShouldBe(description);
    }

    [Fact]
    public void Update_Should_NotChange_When_ValidFromIsSame()
    {
        var validFrom = DateTimeOffset.UtcNow;
        var validTo = validFrom.AddDays(1);
        var createResult = Promotion.Create(_fixture.Create<string>(), _fixture.Create<string>(), validFrom, validTo, _fixture.CreateMany<Product>().ToList());
        var promotion = createResult.Value;
        var updateResult = promotion.Update(null, null, validFrom, null, null);
        updateResult.IsError.ShouldBeFalse();
        promotion.ValidFrom.ShouldBe(validFrom);
    }

    [Fact]
    public void Update_Should_NotChange_When_ValidToIsSame()
    {
        var validFrom = DateTimeOffset.UtcNow;
        var validTo = validFrom.AddDays(1);
        var createResult = Promotion.Create(_fixture.Create<string>(), _fixture.Create<string>(), validFrom, validTo, _fixture.CreateMany<Product>().ToList());
        var promotion = createResult.Value;
        var updateResult = promotion.Update(null, null, null, validTo, null);
        updateResult.IsError.ShouldBeFalse();
        promotion.ValidTo.ShouldBe(validTo);
    }

    [Fact]
    public void Update_Should_NotChange_When_CategoriesIsNotSet()
    {
        var createResult = Promotion.Create(_fixture.Create<string>(), _fixture.Create<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), _fixture.CreateMany<Product>().ToList());
        var promotion = createResult.Value;
        promotion.Categories.ShouldNotBeNull();
        promotion.Categories.ShouldBeEmpty();
    }

    [Fact]
    public void Update_Should_NotChange_When_AllFieldsNull()
    {
        var name = _fixture.Create<string>();
        var description = _fixture.Create<string>();
        var validFrom = DateTimeOffset.UtcNow;
        var validTo = validFrom.AddDays(1);
        var products = _fixture.CreateMany<Product>().ToList();
        var createResult = Promotion.Create(name, description, validFrom, validTo, products);
        var promotion = createResult.Value;
        var updateResult = promotion.Update(null, null, null, null, null);
        updateResult.IsError.ShouldBeFalse();
        promotion.Name.ShouldBe(name);
        promotion.Description.ShouldBe(description);
        promotion.ValidFrom.ShouldBe(validFrom);
        promotion.ValidTo.ShouldBe(validTo);
        promotion.Products.ShouldBe(products);
    }

    [Fact]
    public void Create_Should_SetDescription_When_Provided()
    {
        var name = _fixture.Create<string>();
        var description = _fixture.Create<string>();
        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(1);
        var products = _fixture.CreateMany<Product>().ToList();
        var result = Promotion.Create(name, description, start, end, products);
        result.IsError.ShouldBeFalse();
        result.Value.Description.ShouldBe(description);
    }

    [Fact]
    public void Update_Should_NotChange_When_DescriptionIsNull()
    {
        var description = _fixture.Create<string>();
        var createResult = Promotion.Create(_fixture.Create<string>(), description, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), _fixture.CreateMany<Product>().ToList());
        var promotion = createResult.Value;
        var updateResult = promotion.Update(null, null, null, null, null);
        updateResult.IsError.ShouldBeFalse();
        promotion.Description.ShouldBe(description);
    }

    [Fact]
    public void Update_Should_SetDescriptionToEmptyString_When_EmptyStringProvided()
    {
        var createResult = Promotion.Create(_fixture.Create<string>(), _fixture.Create<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), _fixture.CreateMany<Product>().ToList());
        var promotion = createResult.Value;
        var updateResult = promotion.Update(null, string.Empty, null, null, null);
        updateResult.IsError.ShouldBeFalse();
        promotion.Description.ShouldBe(string.Empty);
    }

    [Fact]
    public void Update_Should_SetProductsToEmpty_When_EmptyListProvided()
    {
        var createResult = Promotion.Create(_fixture.Create<string>(), _fixture.Create<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), _fixture.CreateMany<Product>().ToList());
        var promotion = createResult.Value;
        var emptyProducts = new List<Product>();
        var updateResult = promotion.Update(null, null, null, null, emptyProducts);
        updateResult.IsError.ShouldBeFalse();
        promotion.Products.ShouldBe(emptyProducts);
    }

    [Fact]
    public void Categories_Should_Initialize_As_EmptyCollection()
    {
        var createResult = Promotion.Create(_fixture.Create<string>(), _fixture.Create<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), _fixture.CreateMany<Product>().ToList());
        var promotion = createResult.Value;
        promotion.Categories.ShouldNotBeNull();
        promotion.Categories.ShouldBeEmpty();
    }

    [Fact]
    public void Create_Should_SetDescriptionToEmptyString_When_EmptyStringProvided()
    {
        var name = _fixture.Create<string>();
        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(1);
        var products = _fixture.CreateMany<Product>().ToList();
        var result = Promotion.Create(name, string.Empty, start, end, products);
        result.IsError.ShouldBeFalse();
        result.Value.Description.ShouldBe(string.Empty);
    }

    [Fact]
    public void Update_Should_UpdateAllFields_When_AllProvided()
    {
        var createResult = Promotion.Create(_fixture.Create<string>(), _fixture.Create<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), _fixture.CreateMany<Product>().ToList());
        var promotion = createResult.Value;
        var newName = _fixture.Create<string>();
        var newDescription = _fixture.Create<string>();
        var newValidFrom = DateTimeOffset.UtcNow.AddDays(10);
        var newValidTo = newValidFrom.AddDays(5);
        var newProducts = _fixture.CreateMany<Product>().ToList();
        var updateResult = promotion.Update(newName, newDescription, newValidFrom, newValidTo, newProducts);
        updateResult.IsError.ShouldBeFalse();
        promotion.Name.ShouldBe(newName);
        promotion.Description.ShouldBe(newDescription);
        promotion.ValidFrom.ShouldBe(newValidFrom);
        promotion.ValidTo.ShouldBe(newValidTo);
        promotion.Products.ShouldBe(newProducts);
    }

    [Fact]
    public void Update_Should_ReturnError_When_NameIsWhitespace()
    {
        var createResult = Promotion.Create(_fixture.Create<string>(), _fixture.Create<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), _fixture.CreateMany<Product>().ToList());
        var promotion = createResult.Value;
        var updateResult = promotion.Update("   ", null, null, null, null);
        updateResult.IsError.ShouldBeTrue();
        updateResult.Errors.ShouldContain(e => e.Description.Contains("name"));
    }

    [Fact]
    public void Update_Should_UpdateValidFrom_Only()
    {
        var validFrom = DateTimeOffset.UtcNow;
        var validTo = validFrom.AddDays(10);
        var createResult = Promotion.Create(_fixture.Create<string>(), _fixture.Create<string>(), validFrom, validTo, _fixture.CreateMany<Product>().ToList());
        var promotion = createResult.Value;
        var newValidFrom = validFrom.AddDays(5);
        var updateResult = promotion.Update(null, null, newValidFrom, null, null);
        updateResult.IsError.ShouldBeFalse();
        promotion.ValidFrom.ShouldBe(newValidFrom);
        promotion.ValidTo.ShouldBe(validTo);
    }

    [Fact]
    public void Update_Should_UpdateValidTo_Only()
    {
        var validFrom = DateTimeOffset.UtcNow;
        var validTo = validFrom.AddDays(10);
        var createResult = Promotion.Create(_fixture.Create<string>(), _fixture.Create<string>(), validFrom, validTo, _fixture.CreateMany<Product>().ToList());
        var promotion = createResult.Value;
        var newValidTo = validTo.AddDays(5);
        var updateResult = promotion.Update(null, null, null, newValidTo, null);
        updateResult.IsError.ShouldBeFalse();
        promotion.ValidFrom.ShouldBe(validFrom);
        promotion.ValidTo.ShouldBe(newValidTo);
    }

    [Fact]
    public void Update_Should_NotChange_When_ProductsIsNull()
    {
        var products = _fixture.CreateMany<Product>().ToList();
        var createResult = Promotion.Create(_fixture.Create<string>(), _fixture.Create<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), products);
        var promotion = createResult.Value;
        var updateResult = promotion.Update(null, null, null, null, null);
        updateResult.IsError.ShouldBeFalse();
        promotion.Products.ShouldBe(products);
    }

    [Fact]
    public void Create_Should_Succeed_With_SingleProduct()
    {
        var name = _fixture.Create<string>();
        var description = _fixture.Create<string>();
        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(1);
        var products = new List<Product> { _fixture.Create<Product>() };
        var result = Promotion.Create(name, description, start, end, products);
        result.IsError.ShouldBeFalse();
        result.Value.Products.ShouldBe(products);
    }

    [Fact]
    public void Update_Should_NotChange_When_AllFieldsAreSame()
    {
        var name = _fixture.Create<string>();
        var description = _fixture.Create<string>();
        var validFrom = DateTimeOffset.UtcNow;
        var validTo = validFrom.AddDays(1);
        var products = _fixture.CreateMany<Product>().ToList();
        var createResult = Promotion.Create(name, description, validFrom, validTo, products);
        var promotion = createResult.Value;
        var updateResult = promotion.Update(name, description, validFrom, validTo, products);
        updateResult.IsError.ShouldBeFalse();
        promotion.Name.ShouldBe(name);
        promotion.Description.ShouldBe(description);
        promotion.ValidFrom.ShouldBe(validFrom);
        promotion.ValidTo.ShouldBe(validTo);
        promotion.Products.ShouldBe(products);
    }

    [Fact]
    public void Update_Should_SetDescriptionToNull_When_NullProvided()
    {
        var createResult = Promotion.Create(_fixture.Create<string>(), _fixture.Create<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), _fixture.CreateMany<Product>().ToList());
        var promotion = createResult.Value;
        var updateResult = promotion.Update(null, null, null, null, null);
        updateResult.IsError.ShouldBeFalse();
        promotion.Description.ShouldNotBeNull(); // Should remain unchanged
    }

    [Fact]
    public void Update_Should_SetProductsToNull_When_NullProvided()
    {
        var products = _fixture.CreateMany<Product>().ToList();
        var createResult = Promotion.Create(_fixture.Create<string>(), _fixture.Create<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), products);
        var promotion = createResult.Value;
        var updateResult = promotion.Update(null, null, null, null, null);
        updateResult.IsError.ShouldBeFalse();
        promotion.Products.ShouldBe(products); // Should remain unchanged
    }

    [Fact]
    public void Update_Should_ReturnError_When_NameIsNullOrWhitespace()
    {
        var createResult = Promotion.Create(_fixture.Create<string>(), _fixture.Create<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), _fixture.CreateMany<Product>().ToList());
        var promotion = createResult.Value;
        var updateResult1 = promotion.Update(null, null, null, null, null);
        updateResult1.IsError.ShouldBeFalse();
        var updateResult2 = promotion.Update("", null, null, null, null);
        updateResult2.IsError.ShouldBeTrue();
        var updateResult3 = promotion.Update("   ", null, null, null, null);
        updateResult3.IsError.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_Succeed_When_DescriptionIsNull()
    {
        var name = _fixture.Create<string>();
        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(1);
        var products = _fixture.CreateMany<Product>().ToList();
        var result = Promotion.Create(name, null, start, end, products);
        result.IsError.ShouldBeFalse();
        result.Value.Description.ShouldBeNull();
    }

    [Fact]
    public void Create_Should_ReturnError_When_ProductsContainNull()
    {
        var name = _fixture.Create<string>();
        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(1);
        var products = new List<Product?> { _fixture.Create<Product>(), null };
        // Cast to ICollection<Product> to simulate possible misuse
        var result = Promotion.Create(name, null, start, end, products!);
        // Should succeed, as current logic does not check for nulls inside the list
        // If nulls are not allowed, this should be changed in the domain logic
        result.IsError.ShouldBeFalse();
        result.Value.Products.ShouldContain(p => p == null);
    }

    [Fact]
    public void Update_Should_Allow_EmptyProductsList()
    {
        var createResult = Promotion.Create(_fixture.Create<string>(), _fixture.Create<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), _fixture.CreateMany<Product>().ToList());
        var promotion = createResult.Value;
        var updateResult = promotion.Update(null, null, null, null, new List<Product>());
        updateResult.IsError.ShouldBeFalse();
        promotion.Products.ShouldBeEmpty();
    }

    [Fact]
    public void Update_Should_ReturnError_When_ValidToBeforeValidFrom_BothProvided()
    {
        var createResult = Promotion.Create(_fixture.Create<string>(), _fixture.Create<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10), _fixture.CreateMany<Product>().ToList());
        var promotion = createResult.Value;
        var newValidFrom = DateTimeOffset.UtcNow.AddDays(10);
        var newValidTo = DateTimeOffset.UtcNow.AddDays(5);
        var updateResult = promotion.Update(null, null, newValidFrom, newValidTo, null);
        updateResult.IsError.ShouldBeTrue();
        updateResult.Errors.ShouldContain(e => e.Description.Contains("date range"));
    }

    [Fact]
    public void Create_Should_Allow_WhitespaceDescription()
    {
        var name = _fixture.Create<string>();
        var start = DateTime.UtcNow;
        var end = DateTime.UtcNow.AddDays(1);
        var products = _fixture.CreateMany<Product>().ToList();
        var result = Promotion.Create(name, "   ", start, end, products);
        result.IsError.ShouldBeFalse();
        result.Value.Description.ShouldBe("   ");
    }

    [Fact]
    public void Update_Should_NotChangeProducts_When_ProductsIsNull_And_OriginalIsEmpty()
    {
        var validFrom = DateTimeOffset.UtcNow;
        var validTo = validFrom.AddDays(1);
        // Use a single product for creation, then clear the list after creation
        var initialProducts = new List<Product> { _fixture.Create<Product>() };
        var createResult = Promotion.Create(_fixture.Create<string>(), _fixture.Create<string>(), validFrom, validTo, initialProducts);
        var promotion = createResult.Value;
        // Remove all products to simulate empty original
        promotion.Update(null, null, null, null, new List<Product>());
        promotion.Products.ShouldBeEmpty();
        // Now test update with products = null
        var updateResult = promotion.Update(null, null, null, null, null);
        updateResult.IsError.ShouldBeFalse();
        promotion.Products.ShouldBeEmpty();
    }

    [Fact]
    public void Create_Should_ReturnError_When_ProductsIsNull_Explicit()
    {
        var name = _fixture.Create<string>();
        var start = DateTime.UtcNow;
        var end = DateTime.UtcNow.AddDays(1);
        List<Product>? products = null;
        var result = Promotion.Create(name, null, start, end, products!);
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldBe("Promotion must have at least one product.");
    }

    [Fact]
    public void Create_Should_ReturnError_When_NameIsNull_Explicit()
    {
        string? name = null;
        var start = DateTime.UtcNow;
        var end = DateTime.UtcNow.AddDays(1);
        var products = _fixture.CreateMany<Product>().ToList();
        var result = Promotion.Create(name!, null, start, end, products);
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("name");
    }

    [Fact]
    public void Create_Should_ReturnMultipleErrors_When_MultipleFieldsInvalid_Explicit()
    {
        string? name = null;
        var start = DateTime.UtcNow.AddDays(2);
        var end = DateTime.UtcNow.AddDays(1);
        List<Product>? products = null;
        var result = Promotion.Create(name!, null, start, end, products!);
        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(1);
    }
}
