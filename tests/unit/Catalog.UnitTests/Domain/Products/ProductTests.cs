#nullable enable
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Catalog.Domain.Entities.BrandAggregate;
using Catalog.Domain.Entities.CategoryAggregate;
using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Domain.Entities.ProductAggregate.Events;
using Shouldly;
using System.Globalization;

namespace Catalog.UnitTests.Domain.Products;

public class ProductTests
{
    private readonly IFixture _fixture;

    public ProductTests()
    {
        _fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
    }

    [Fact]
    public void Create_Should_SetCorrectProperties()
    {
        // Arrange
        var name = _fixture.Create<string>();
        var description = _fixture.Create<string>();
        var sku = _fixture.Create<string>();
        var gtin = _fixture.Create<string>();
        var categories = _fixture.CreateMany<Category>().ToList();
        var brand = _fixture.Create<Brand>();

        // Act
        var result = Product.Create(
            name,
            description,
            sku,
            gtin,
            categories,
            isActive: true,
            brand.Id);

        // Assert
        result.IsError.ShouldBeFalse();
        var product = result.Value;
        product.Name.ShouldBe(name);
        product.Description.ShouldBe(description);
        product.ProductSKU.ShouldBe(sku);
        product.GTIN.ShouldBe(gtin);
        product.Categories.ShouldBe(categories);
        product.IsActive.ShouldBeTrue();
        product.BrandId.ShouldBe(brand.Id);
        product.Slug.ShouldBe(name.ToLower(CultureInfo.InvariantCulture).Replace(" ", "-", StringComparison.Ordinal));
        product.DomainEvents.ShouldContain(e => e is ProductCreatedDomainEvent);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_Should_ReturnError_When_NameIsInvalid(string? invalidName)
    {
        // Arrange
        var description = _fixture.Create<string>();
        var sku = _fixture.Create<string>();
        var gtin = _fixture.Create<string>();
        var categories = _fixture.CreateMany<Category>().ToList();
        var brand = _fixture.Create<Brand>();

        // Act
        var result = Product.Create(
            invalidName!,
            description,
            sku,
            gtin,
            categories,
            isActive: true,
            brand.Id);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("cannot be empty");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_ReturnError_When_DescriptionIsInvalid(string? invalidDescription)
    {
        // Arrange
        var name = _fixture.Create<string>();
        var sku = _fixture.Create<string>();
        var gtin = _fixture.Create<string>();
        var categories = _fixture.CreateMany<Category>().ToList();
        var brand = _fixture.Create<Brand>();

        // Act
        var result = Product.Create(name, invalidDescription, sku, gtin, categories, true, brand.Id);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("description");
    }

    [Fact]
    public void Create_Should_ReturnError_When_DescriptionIsWhitespace()
    {
        // Arrange
        var name = _fixture.Create<string>();
        var sku = _fixture.Create<string>();
        var gtin = _fixture.Create<string>();
        var categories = _fixture.CreateMany<Category>().ToList();
        var brand = _fixture.Create<Brand>();

        // Act
        var result = Product.Create(name, "   ", sku, gtin, categories, true, brand.Id);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("description");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_ReturnError_When_SKUIsInvalid(string? invalidSku)
    {
        // Arrange
        var name = _fixture.Create<string>();
        var description = _fixture.Create<string>();
        var gtin = _fixture.Create<string>();
        var categories = _fixture.CreateMany<Category>().ToList();
        var brand = _fixture.Create<Brand>();

        // Act
        var result = Product.Create(name, description, invalidSku, gtin, categories, true, brand.Id);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("SKU");
    }

    [Fact]
    public void Create_Should_ReturnError_When_SKUIsWhitespace()
    {
        // Arrange
        var name = _fixture.Create<string>();
        var description = _fixture.Create<string>();
        var gtin = _fixture.Create<string>();
        var categories = _fixture.CreateMany<Category>().ToList();
        var brand = _fixture.Create<Brand>();

        // Act
        var result = Product.Create(name, description, "   ", gtin, categories, true, brand.Id);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("SKU");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_Should_Allow_Optional_GTIN(string? optionalGtin)
    {
        // Arrange
        var name = _fixture.Create<string>();
        var description = _fixture.Create<string>();
        var sku = _fixture.Create<string>();
        var categories = _fixture.CreateMany<Category>().ToList();
        var brand = _fixture.Create<Brand>();

        // Act
        var result = Product.Create(name, description, sku, optionalGtin, categories, true, brand.Id);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.GTIN.ShouldBe(optionalGtin);
    }

    [Fact]
    public void Create_Should_GenerateValidSlug()
    {
        // Arrange
        var name = "Product Name With Spaces";
        var description = _fixture.Create<string>();
        var sku = _fixture.Create<string>();
        var gtin = _fixture.Create<string>();
        var categories = _fixture.CreateMany<Category>().ToList();
        var brand = _fixture.Create<Brand>();

        // Act
        var result = Product.Create(name, description, sku, gtin, categories, true, brand.Id);

        // Assert
        result.IsError.ShouldBeFalse();
        var product = result.Value;
        product.Slug.ShouldBe("product-name-with-spaces");
    }

    [Fact]
    public void Update_Should_OnlyModifyChangedProperties()
    {
        // Arrange
        var createResult = Product.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.CreateMany<Category>().ToList(),
            true,
            _fixture.Create<Brand>().Id
        );
        var product = createResult.Value;
        var originalDescription = product.Description;
        var newName = _fixture.Create<string>();

        // Act
        var updateResult = product.Update(newName);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        product.Name.ShouldBe(newName);
        product.Description.ShouldBe(originalDescription);
        product.Slug.ShouldBe(newName.ToLower(CultureInfo.InvariantCulture).Replace(" ", "-", StringComparison.Ordinal));
    }

    [Fact]
    public void Update_Should_ReturnError_When_NameIsEmpty()
    {
        // Arrange
        var createResult = Product.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.CreateMany<Category>().ToList(),
            true,
            _fixture.Create<Brand>().Id
        );
        var product = createResult.Value;

        // Act
        var updateResult = product.Update("");

        // Assert
        updateResult.IsError.ShouldBeTrue();
    }

    [Fact]
    public void Update_Should_DoNothing_When_NameIsNull()
    {
        // Arrange
        var createResult = Product.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.CreateMany<Category>().ToList(),
            true,
            _fixture.Create<Brand>().Id
        );
        var product = createResult.Value;
        var originalName = product.Name;
        var originalSlug = product.Slug;

        // Act
        var updateResult = product.Update(null);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        product.Name.ShouldBe(originalName);
        product.Slug.ShouldBe(originalSlug);
    }

    [Fact]
    public void Update_Should_DoNothing_When_NameIsSame()
    {
        // Arrange
        var createResult = Product.Create(
            "TestName",
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.CreateMany<Category>().ToList(),
            true,
            _fixture.Create<Brand>().Id
        );
        var product = createResult.Value;
        var originalSlug = product.Slug;

        // Act
        var updateResult = product.Update("TestName");

        // Assert
        updateResult.IsError.ShouldBeFalse();
        product.Slug.ShouldBe(originalSlug);
    }

    [Theory]
    [InlineData("  My Product!  ", "my-product")]
    [InlineData("Product--Name", "product-name")]
    [InlineData("Product___Name", "product-name")]
    [InlineData("Product", "product")]
    [InlineData("  ", "")]
    public void GetProductSlug_Should_ProduceExpectedSlug(string input, string expected)
    {
        var method = typeof(Product).GetMethod("GetProductSlug", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.ShouldNotBeNull();
        var slug = method.Invoke(null, new object[] { input }) as string;
        slug.ShouldNotBeNull();
        slug.ShouldBe(expected);
    }

    // Uncomment and adapt these if your Product aggregate supports AddCategory/RemoveCategory with ErrorOr
    /*
    [Fact]
    public void AddCategory_Should_AddToCategories()
    {
        // Arrange
        var createResult = Product.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            new List<Category>(),
            true,
            _fixture.Create<Brand>()
        );
        var product = createResult.Value;
        var category = _fixture.Create<Category>();

        // Act
        var addResult = product.AddCategory(category);

        // Assert
        addResult.IsError.ShouldBeFalse();
        product.Categories.ShouldContain(category);
    }

    [Fact]
    public void RemoveCategory_Should_RemoveFromCategories()
    {
        // Arrange
        var category = _fixture.Create<Category>();
        var createResult = Product.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            new List<Category> { category },
            true,
            _fixture.Create<Brand>()
        );
        var product = createResult.Value;

        // Act
        var removeResult = product.RemoveCategory(category);

        // Assert
        removeResult.IsError.ShouldBeFalse();
        product.Categories.ShouldNotContain(category);
    }
    */

    [Fact]
    public void Create_Should_ReturnMultipleErrors_When_MultipleFieldsInvalid()
    {
        // Arrange
        string? name = null;
        string? description = null;
        string? sku = null;
        string? gtin = null;
        List<Category>? categories = null;

        // Act
        var result = Product.Create(name!, description, sku, gtin, categories!, true);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Update_Should_Succeed_When_NameIsNull()
    {
        // Arrange
        var createResult = Product.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.CreateMany<Category>().ToList(),
            true,
            _fixture.Create<Brand>().Id
        );
        var product = createResult.Value;
        var originalName = product.Name;
        var originalSlug = product.Slug;

        // Act
        var updateResult = product.Update(null);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        product.Name.ShouldBe(originalName);
        product.Slug.ShouldBe(originalSlug);
    }

    [Fact]
    public void Update_Should_ReturnError_When_NameIsWhitespace()
    {
        // Arrange
        var createResult = Product.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.CreateMany<Category>().ToList(),
            true,
            _fixture.Create<Brand>().Id
        );
        var product = createResult.Value;

        // Act
        var updateResult = product.Update("   ");

        // Assert
        updateResult.IsError.ShouldBeTrue();
        updateResult.Errors.ShouldContain(e => e.Description.Contains("cannot be empty"));
    }

    [Fact]
    public void Update_Should_ReturnMultipleErrors_When_NameIsNullOrWhitespace()
    {
        // Arrange
        var createResult = Product.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.CreateMany<Category>().ToList(),
            true,
            _fixture.Create<Brand>().Id
        );
        var product = createResult.Value;

        // Act
        var updateResult1 = product.Update(null);
        var updateResult2 = product.Update("");
        var updateResult3 = product.Update("   ");

        // Assert
        updateResult1.IsError.ShouldBeFalse();
        updateResult2.IsError.ShouldBeTrue();
        updateResult3.IsError.ShouldBeTrue();
    }
}