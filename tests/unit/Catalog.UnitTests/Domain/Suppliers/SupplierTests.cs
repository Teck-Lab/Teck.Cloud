using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Catalog.Domain.Entities.SupplierAggregate;
using Shouldly;

namespace Catalog.UnitTests.Domain.Suppliers;

public class SupplierTests
{
    private readonly IFixture _fixture;

    public SupplierTests()
    {
        _fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
    }

    [Fact]
    public void Create_Should_SetCorrectProperties()
    {
        // Arrange
        var name = _fixture.Create<string>();
        var description = _fixture.Create<string>();
        var website = "https://example.com"; // Use a valid absolute URL

        // Act
        var result = Supplier.Create(name, description, website);

        // Assert
        result.IsError.ShouldBeFalse();
        var supplier = result.Value;
        supplier.Name.ShouldBe(name);
        supplier.Description.ShouldBe(description);
        supplier.Website.ShouldBe(website);
    }

    [Fact]
    public void Update_Should_OnlyModifyChangedProperties()
    {
        // Arrange
        var createResult = Supplier.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            "https://example.com"
        );
        var supplier = createResult.Value;
        var originalName = supplier.Name;
        var newDescription = _fixture.Create<string>();
        var newWebsite = "https://newsite.com";

        // Act
        var updateResult = supplier.Update(null, newDescription, newWebsite);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        supplier.Name.ShouldBe(originalName);
        supplier.Description.ShouldBe(newDescription);
        supplier.Website.ShouldBe(newWebsite);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_ReturnError_When_NameIsInvalid(string? invalidName)
    {
        // Arrange
        var website = "https://example.com";

        // Act
        var result = Supplier.Create(invalidName!, website, null);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_ReturnError_When_WebsiteIsInvalid(string? invalidWebsite)
    {
        // Arrange
        var name = _fixture.Create<string>();

        // Act
        var result = Supplier.Create(name, invalidWebsite!, null);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Description.Contains("website"));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://badurl")]
    [InlineData("http:/missing-slash.com")]
    public void Create_Should_ReturnError_When_WebsiteIsMalformed(string badWebsite)
    {
        // Arrange
        var name = _fixture.Create<string>();

        // Act
        var result = Supplier.Create(name, null, badWebsite);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Supplier.InvalidWebsite");
    }

    [Fact]
    public void Create_Should_ReturnMultipleErrors_When_MultipleFieldsInvalid()
    {
        // Arrange
        string name = null!;
        string website = null!;

        // Act
        var result = Supplier.Create(name, null, website);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Create_Should_Succeed_With_Uppercase_Http_Or_Https()
    {
        // Arrange
        var name = _fixture.Create<string>();
        var website = "HTTPS://EXAMPLE.COM";

        // Act
        var result = Supplier.Create(name, null, website);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Website.ShouldBe(website);
    }

    [Fact]
    public void Update_Should_Succeed_When_AllFieldsNull()
    {
        // Arrange
        var createResult = Supplier.Create(_fixture.Create<string>(), _fixture.Create<string>(), "https://example.com");
        var supplier = createResult.Value;
        var originalName = supplier.Name;
        var originalDescription = supplier.Description;
        var originalWebsite = supplier.Website;

        // Act
        var updateResult = supplier.Update(null, null, null);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        supplier.Name.ShouldBe(originalName);
        supplier.Description.ShouldBe(originalDescription);
        supplier.Website.ShouldBe(originalWebsite);
    }

    [Fact]
    public void Update_Should_ReturnError_When_NameIsEmpty()
    {
        // Arrange
        var createResult = Supplier.Create(_fixture.Create<string>(), _fixture.Create<string>(), "https://example.com");
        var supplier = createResult.Value;

        // Act
        var updateResult = supplier.Update("", null, null);

        // Assert
        updateResult.IsError.ShouldBeTrue();
        updateResult.Errors.ShouldContain(e => e.Description.Contains("name"));
    }

    [Fact]
    public void Update_Should_ReturnError_When_WebsiteIsInvalid()
    {
        // Arrange
        var createResult = Supplier.Create(_fixture.Create<string>(), _fixture.Create<string>(), "https://example.com");
        var supplier = createResult.Value;
        var invalidUrl = "not-a-valid-url";

        // Act
        var updateResult = supplier.Update(null, null, invalidUrl);

        // Assert
        updateResult.IsError.ShouldBeTrue();
        updateResult.Errors.ShouldContain(e => e.Description.Contains("valid URL"));
    }

    [Fact]
    public void Update_Should_SetWebsite_When_ValidUrlProvided()
    {
        // Arrange
        var createResult = Supplier.Create(_fixture.Create<string>(), _fixture.Create<string>(), "https://example.com");
        var supplier = createResult.Value;
        var newWebsite = "https://newsite.com";

        // Act
        var updateResult = supplier.Update(null, null, newWebsite);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        supplier.Website.ShouldBe(newWebsite);
    }

    [Fact]
    public void Update_Should_NotChange_When_ValuesAreSame()
    {
        // Arrange
        var name = _fixture.Create<string>();
        var description = _fixture.Create<string>();
        var website = "https://example.com";
        var createResult = Supplier.Create(name, description, website);
        var supplier = createResult.Value;

        // Act
        var updateResult = supplier.Update(name, description, website);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        supplier.Name.ShouldBe(name);
        supplier.Description.ShouldBe(description);
        supplier.Website.ShouldBe(website);
    }

    [Fact]
    public void Update_Should_ReturnMultipleErrors_When_MultipleFieldsInvalid()
    {
        // Arrange
        var createResult = Supplier.Create(_fixture.Create<string>(), _fixture.Create<string>(), "https://example.com");
        var supplier = createResult.Value;

        // Act
        var updateResult = supplier.Update("", "", "bad-url");

        // Assert
        updateResult.IsError.ShouldBeTrue();
        updateResult.Errors.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Create_Should_Succeed_When_DescriptionIsNull()
    {
        // Arrange
        var name = _fixture.Create<string>();
        var website = "https://example.com";

        // Act
        var result = Supplier.Create(name, null, website);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Description.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_ReturnError_When_DescriptionIsEmpty(string invalidDescription)
    {
        // Arrange
        var name = _fixture.Create<string>();
        var website = "https://example.com";

        // Act
        var result = Supplier.Create(name, invalidDescription, website);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Description.Contains("description"));
    }

    [Fact]
    public void Create_Should_ReturnMultipleErrors_When_AllFieldsInvalid()
    {
        // Arrange
        string name = null!;
        string description = " ";
        string website = null!;

        // Act
        var result = Supplier.Create(name, description, website);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(2);
    }

    [Fact]
    public void Update_Should_ReturnError_When_DescriptionIsEmpty()
    {
        // Arrange
        var createResult = Supplier.Create(_fixture.Create<string>(), _fixture.Create<string>(), "https://example.com");
        var supplier = createResult.Value;

        // Act
        var updateResult = supplier.Update(null, "", null);

        // Assert
        updateResult.IsError.ShouldBeTrue();
        updateResult.Errors.ShouldContain(e => e.Description.Contains("description"));
    }

    [Fact]
    public void Update_Should_ChangeDescription_When_NewValueProvided()
    {
        // Arrange
        var createResult = Supplier.Create(_fixture.Create<string>(), _fixture.Create<string>(), "https://example.com");
        var supplier = createResult.Value;
        var newDescription = _fixture.Create<string>();

        // Act
        var updateResult = supplier.Update(null, newDescription, null);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        supplier.Description.ShouldBe(newDescription);
    }

    [Fact]
    public void Update_Should_NotChange_When_DescriptionIsSame()
    {
        // Arrange
        var description = _fixture.Create<string>();
        var createResult = Supplier.Create(_fixture.Create<string>(), description, "https://example.com");
        var supplier = createResult.Value;

        // Act
        var updateResult = supplier.Update(null, description, null);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        supplier.Description.ShouldBe(description);
    }

    [Fact]
    public void Update_Should_NotChange_When_WebsiteIsSame()
    {
        // Arrange
        var website = "https://example.com";
        var createResult = Supplier.Create(_fixture.Create<string>(), _fixture.Create<string>(), website);
        var supplier = createResult.Value;

        // Act
        var updateResult = supplier.Update(null, null, website);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        supplier.Website.ShouldBe(website);
    }

    [Fact]
    public void Update_Should_ReturnError_When_WebsiteIsWhitespace()
    {
        // Arrange
        var createResult = Supplier.Create(_fixture.Create<string>(), _fixture.Create<string>(), "https://example.com");
        var supplier = createResult.Value;

        // Act
        var updateResult = supplier.Update(null, null, "   ");

        // Assert
        updateResult.IsError.ShouldBeTrue();
        updateResult.Errors.ShouldContain(e => e.Description.Contains("website"));
    }

    [Fact]
    public void Update_Should_ReturnError_When_WebsiteIsMalformed()
    {
        // Arrange
        var createResult = Supplier.Create(_fixture.Create<string>(), _fixture.Create<string>(), "https://example.com");
        var supplier = createResult.Value;

        // Act
        var updateResult = supplier.Update(null, null, "not-a-url");

        // Assert
        updateResult.IsError.ShouldBeTrue();
        updateResult.Errors.ShouldContain(e => e.Description.Contains("valid URL"));
    }

    [Fact]
    public void Update_Should_SetWebsite_When_PreviouslyNull()
    {
        // Arrange
        var createResult = Supplier.Create(_fixture.Create<string>(), _fixture.Create<string>(), "https://example.com");
        var supplier = createResult.Value;
        supplier.Update(null, null, null); // set to null
        var newWebsite = "https://newsite.com";

        // Act
        var updateResult = supplier.Update(null, null, newWebsite);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        supplier.Website.ShouldBe(newWebsite);
    }

    [Fact]
    public void Update_Should_UpdateAllFields_When_AllProvided()
    {
        // Arrange
        var createResult = Supplier.Create(_fixture.Create<string>(), _fixture.Create<string>(), "https://example.com");
        var supplier = createResult.Value;
        var newName = _fixture.Create<string>();
        var newDescription = _fixture.Create<string>();
        var newWebsite = "https://newsite.com";

        // Act
        var updateResult = supplier.Update(newName, newDescription, newWebsite);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        supplier.Name.ShouldBe(newName);
        supplier.Description.ShouldBe(newDescription);
        supplier.Website.ShouldBe(newWebsite);
    }

    [Fact]
    public void Create_Should_ReturnError_When_WebsiteIsWhitespace()
    {
        // Arrange
        var name = _fixture.Create<string>();
        var website = "   ";

        // Act
        var result = Supplier.Create(name, null, website);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Description.Contains("website"));
    }

    [Fact]
    public void Create_Should_ReturnError_When_DescriptionIsWhitespace()
    {
        // Arrange
        var name = _fixture.Create<string>();
        var website = "https://example.com";

        // Act
        var result = Supplier.Create(name, "   ", website);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Description.Contains("description"));
    }

    [Fact]
    public void Create_Should_ReturnError_When_NameIsWhitespace()
    {
        // Arrange
        var website = "https://example.com";

        // Act
        var result = Supplier.Create("   ", null, website);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Description.Contains("name"));
    }

    [Fact]
    public void Create_Should_ReturnError_When_WebsiteIsNull()
    {
        // Arrange
        var name = _fixture.Create<string>();

        // Act
        var result = Supplier.Create(name, null, null);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Description.Contains("website"));
    }
}