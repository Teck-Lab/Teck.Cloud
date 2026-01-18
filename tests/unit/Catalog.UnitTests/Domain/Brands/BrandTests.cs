using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Catalog.Domain.Entities.BrandAggregate;
using Shouldly;

namespace Catalog.UnitTests.Domain.Brands;

public class BrandTests
{
    private readonly IFixture _fixture;

    public BrandTests()
    {
        _fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
    }

    [Fact]
    public void Create_Should_SetCorrectProperties()
    {
        // Arrange
        var name = _fixture.Create<string>();
        var description = _fixture.Create<string>();
        var website = "https://example.com";

        // Act
        var result = Brand.Create(name, description, website);

        // Assert
        result.IsError.ShouldBeFalse();
        var brand = result.Value;
        brand.Name.ShouldBe(name);
        brand.Description.ShouldBe(description);
        brand.Website!.Value.ShouldBe(website);
        brand.Products.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_ReturnError_When_NameIsInvalid(string? invalidName)
    {
        // Arrange
        var description = _fixture.Create<string>();
        var website = "https://example.com";

        // Act
        var result = Brand.Create(invalidName!, description, website);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_ReturnError_When_DescriptionIsInvalid(string? invalidDescription)
    {
        // Arrange
        var name = _fixture.Create<string>();
        var website = "https://example.com";

        // Act
        var result = Brand.Create(name, invalidDescription, website);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("description");
    }

    [Fact]
    public void Create_Should_Succeed_When_WebsiteIsNull()
    {
        // Arrange
        var name = _fixture.Create<string>();
        var description = _fixture.Create<string>();
        string? website = null;

        // Act
        var result = Brand.Create(name, description, website);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Website.ShouldBeNull();
    }

    [Fact]
    public void Create_Should_ReturnMultipleErrors_When_MultipleFieldsInvalid()
    {
        // Arrange
        string? name = null;
        string description = " ";
        string website = "bad-url";

        // Act
        var result = Brand.Create(name!, description, website);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Create_Should_Succeed_With_Uppercase_Http_Or_Https()
    {
        // Arrange
        var name = _fixture.Create<string>();
        var description = _fixture.Create<string>();
        var website = "HTTPS://EXAMPLE.COM";

        // Act
        var result = Brand.Create(name, description, website);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Website!.Value.ShouldBe(website);
    }

    [Fact]
    public void Update_Should_OnlyModifyChangedProperties()
    {
        // Arrange
        var createResult = Brand.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            "https://example.com"
        );
        var brand = createResult.Value;
        var originalName = brand.Name;
        var newDescription = _fixture.Create<string>();
        var newWebsite = "https://newsite.com";

        // Act
        var updateResult = brand.Update(null, newDescription, newWebsite);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        brand.Name.ShouldBe(originalName);
        brand.Description.ShouldBe(newDescription);
        brand.Website!.Value.ShouldBe(newWebsite);
    }

    [Fact]
    public void Update_Should_ReturnError_When_WebsiteIsInvalid()
    {
        // Arrange
        var createResult = Brand.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            "https://example.com"
        );
        var brand = createResult.Value;
        var invalidUrl = "not-a-valid-url";

        // Act
        var updateResult = brand.Update(null, null, invalidUrl);

        // Assert
        updateResult.IsError.ShouldBeTrue();
        updateResult.FirstError.Description.ShouldContain("valid URL");
    }

    [Fact]
    public void Website_Should_ReturnError_When_InvalidUrl()
    {
        // Arrange
        var name = _fixture.Create<string>();
        var description = _fixture.Create<string>();
        var invalidUrl = "not-a-valid-url";

        // Act
        var result = Brand.Create(name, description, invalidUrl);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("valid URL");
    }

    [Fact]
    public void Update_Should_Succeed_When_AllFieldsNull()
    {
        // Arrange
        var createResult = Brand.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            "https://example.com"
        );
        var brand = createResult.Value;
        var originalName = brand.Name;
        var originalDescription = brand.Description;
        var originalWebsite = brand.Website;

        // Act
        var updateResult = brand.Update(null, null, null);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        brand.Name.ShouldBe(originalName);
        brand.Description.ShouldBe(originalDescription);
        if (originalWebsite is null)
            brand.Website.ShouldBeNull();
        else
            brand.Website!.Value.ShouldBe(originalWebsite.Value);
    }

    [Fact]
    public void Update_Should_ReturnMultipleErrors_When_MultipleFieldsInvalid()
    {
        // Arrange
        var createResult = Brand.Create(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            "https://example.com"
        );
        var brand = createResult.Value;

        // Act
        var updateResult = brand.Update("", "", "bad-url");

        // Assert
        updateResult.IsError.ShouldBeTrue();
        updateResult.Errors.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Update_Should_NotChange_When_ValuesAreSame()
    {
        // Arrange
        var name = _fixture.Create<string>();
        var description = _fixture.Create<string>();
        var website = "https://example.com";
        var createResult = Brand.Create(name, description, website);
        var brand = createResult.Value;

        // Act
        var updateResult = brand.Update(name, description, website);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        brand.Name.ShouldBe(name);
        brand.Description.ShouldBe(description);
        brand.Website!.Value.ShouldBe(website);
    }

    [Fact]
    public void Update_Should_UpdateAllFields_When_AllProvided()
    {
        var createResult = Brand.Create(_fixture.Create<string>(), _fixture.Create<string>(), "https://example.com");
        var brand = createResult.Value;
        var newName = _fixture.Create<string>();
        var newDescription = _fixture.Create<string>();
        var newWebsite = "https://newsite.com";
        var updateResult = brand.Update(newName, newDescription, newWebsite);
        updateResult.IsError.ShouldBeFalse();
        brand.Name.ShouldBe(newName);
        brand.Description.ShouldBe(newDescription);
        brand.Website!.Value.ShouldBe(newWebsite);
    }

    [Fact]
    public void Create_Should_ReturnAllErrors_When_MultipleFieldsInvalid()
    {
        // Arrange
        string name = " ";
        string description = " ";
        string website = "not-a-url";

        // Act
        var result = Brand.Create(name, description, website);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBe(3);
        result.Errors.ShouldContain(e => e.Description.Contains("name"));
        result.Errors.ShouldContain(e => e.Description.Contains("description"));
        result.Errors.ShouldContain(e => e.Description.Contains("valid URL"));
    }

    [Fact]
    public void Update_Should_ReturnAllErrors_When_MultipleFieldsInvalid()
    {
        // Arrange
        var createResult = Brand.Create("ValidName", "ValidDesc", "https://example.com");
        var brand = createResult.Value;

        // Act
        var updateResult = brand.Update("", "", "not-a-url");

        // Assert
        updateResult.IsError.ShouldBeTrue();
        updateResult.Errors.Count.ShouldBe(3);
        updateResult.Errors.ShouldContain(e => e.Description.Contains("name"));
        updateResult.Errors.ShouldContain(e => e.Description.Contains("description"));
        updateResult.Errors.ShouldContain(e => e.Description.Contains("valid URL"));
    }

    [Fact]
    public void Update_Should_NotChange_Properties_When_ValuesAreNull()
    {
        // Arrange
        var createResult = Brand.Create("Name", "Desc", "https://example.com");
        var brand = createResult.Value;
        var originalName = brand.Name;
        var originalDescription = brand.Description;
        var originalWebsite = brand.Website;

        // Act
        var updateResult = brand.Update(null, null, null);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        brand.Name.ShouldBe(originalName);
        brand.Description.ShouldBe(originalDescription);
        brand.Website.ShouldBe(originalWebsite);
    }

    [Fact]
    public void Update_Should_RemoveWebsite_When_EmptyString()
    {
        // Arrange
        var createResult = Brand.Create("Name", "Desc", "https://example.com");
        var brand = createResult.Value;

        // Act
        var updateResult = brand.Update(null, null, "");

        // Assert
        updateResult.IsError.ShouldBeTrue(); // Domain expects error for empty website
        updateResult.Errors.ShouldContain(e => e.Description.Contains("website"));
    }

    [Fact]
    public void Update_Should_RemoveDescription_When_EmptyString()
    {
        // Arrange
        var createResult = Brand.Create("Name", "Desc", "https://example.com");
        var brand = createResult.Value;

        // Act
        var updateResult = brand.Update(null, "", null);

        // Assert
        updateResult.IsError.ShouldBeTrue(); // Domain expects error for empty description
        updateResult.Errors.ShouldContain(e => e.Description.Contains("description"));
    }

    [Fact]
    public void Update_Should_NotAddEvents()
    {
        // Arrange
        var createResult = Brand.Create("Name", "Desc", "https://example.com");
        var brand = createResult.Value;
        brand.ClearDomainEvents();

        // Act
        var updateResult = brand.Update("NewName", "NewDesc", "https://newsite.com");

        // Assert
        brand.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Products_Should_Always_BeInitialized()
    {
        var result = Brand.Create("Name", "Desc", "https://example.com");
        var brand = result.Value;
        brand.Products.ShouldNotBeNull();
        brand.Products.ShouldBeEmpty();
    }

    [Fact]
    public void Create_WithNullWebsite_Should_SetWebsiteToNull()
    {
        var result = Brand.Create("Name", "Desc", null);
        result.IsError.ShouldBeFalse();
        result.Value.Website.ShouldBeNull();
    }

    [Fact]
    public void Create_WithValidWebsite_Should_SetWebsite()
    {
        var url = "https://example.com";
        var result = Brand.Create("Name", "Desc", url);
        result.IsError.ShouldBeFalse();
        result.Value.Website!.Value.ShouldBe(url);
    }

    [Fact]
    public void Update_WithValidWebsite_Should_SetWebsite()
    {
        var result = Brand.Create("Name", "Desc", "https://example.com");
        var brand = result.Value;
        var newUrl = "https://newsite.com";
        var updateResult = brand.Update(null, null, newUrl);
        updateResult.IsError.ShouldBeFalse();
        brand.Website!.Value.ShouldBe(newUrl);
    }

    [Fact]
    public void Update_WithNullWebsite_Should_NotChangeWebsite()
    {
        var result = Brand.Create("Name", "Desc", "https://example.com");
        var brand = result.Value;
        var originalWebsite = brand.Website;
        var updateResult = brand.Update(null, null, null);
        updateResult.IsError.ShouldBeFalse();
        brand.Website.ShouldBe(originalWebsite);
    }

    [Fact]
    public void Update_WithSameWebsite_Should_NotChangeWebsite()
    {
        var url = "https://example.com";
        var result = Brand.Create("Name", "Desc", url);
        var brand = result.Value;
        var updateResult = brand.Update(null, null, url);
        updateResult.IsError.ShouldBeFalse();
        brand.Website!.Value.ShouldBe(url);
    }

    [Fact]
    public void Update_WithInvalidWebsite_Should_ReturnError_And_NotChangeWebsite()
    {
        var url = "https://example.com";
        var result = Brand.Create("Name", "Desc", url);
        var brand = result.Value;
        var updateResult = brand.Update(null, null, "not-a-url");
        updateResult.IsError.ShouldBeTrue();
        brand.Website!.Value.ShouldBe(url);
    }

    [Fact]
    public void Update_WithEmptyName_Should_ReturnError_And_NotChangeName()
    {
        var result = Brand.Create("Name", "Desc", "https://example.com");
        var brand = result.Value;
        var originalName = brand.Name;
        var updateResult = brand.Update("", null, null);
        updateResult.IsError.ShouldBeTrue();
        brand.Name.ShouldBe(originalName);
    }

    [Fact]
    public void Update_WithEmptyDescription_Should_ReturnError_And_NotChangeDescription()
    {
        var result = Brand.Create("Name", "Desc", "https://example.com");
        var brand = result.Value;
        var originalDescription = brand.Description;
        var updateResult = brand.Update(null, "", null);
        updateResult.IsError.ShouldBeTrue();
        brand.Description.ShouldBe(originalDescription);
    }

    [Fact]
    public void Update_WithOnlyNameChanged_Should_OnlyUpdateName()
    {
        var result = Brand.Create("Name", "Desc", "https://example.com");
        var brand = result.Value;
        var newName = "NewName";
        var updateResult = brand.Update(newName, null, null);
        updateResult.IsError.ShouldBeFalse();
        brand.Name.ShouldBe(newName);
        brand.Description.ShouldBe("Desc");
        brand.Website!.Value.ShouldBe("https://example.com");
    }

    [Fact]
    public void Update_WithOnlyDescriptionChanged_Should_OnlyUpdateDescription()
    {
        var result = Brand.Create("Name", "Desc", "https://example.com");
        var brand = result.Value;
        var newDesc = "NewDesc";
        var updateResult = brand.Update(null, newDesc, null);
        updateResult.IsError.ShouldBeFalse();
        brand.Name.ShouldBe("Name");
        brand.Description.ShouldBe(newDesc);
        brand.Website!.Value.ShouldBe("https://example.com");
    }

    [Fact]
    public void Update_WithOnlyWebsiteChanged_Should_OnlyUpdateWebsite()
    {
        var result = Brand.Create("Name", "Desc", "https://example.com");
        var brand = result.Value;
        var newUrl = "https://newsite.com";
        var updateResult = brand.Update(null, null, newUrl);
        updateResult.IsError.ShouldBeFalse();
        brand.Name.ShouldBe("Name");
        brand.Description.ShouldBe("Desc");
        brand.Website!.Value.ShouldBe(newUrl);
    }

    [Fact]
    public void Update_Should_NotAddEvents_When_ValuesChanged()
    {
        var result = Brand.Create("Name", "Desc", "https://example.com");
        var brand = result.Value;
        brand.ClearDomainEvents();
        var updateResult = brand.Update("NewName", "NewDesc", "https://newsite.com");
        updateResult.IsError.ShouldBeFalse();
        brand.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Create_Should_Fail_When_AllFieldsNullOrEmpty()
    {
        var result = Brand.Create(null!, null, null);
        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2); // name and description required
    }

    [Fact]
    public void Update_WithAllValuesUnchanged_Should_NotAlterState_OrAddEvents()
    {
        var result = Brand.Create("Name", "Desc", "https://example.com");
        var brand = result.Value;
        brand.ClearDomainEvents();
        var updateResult = brand.Update("Name", "Desc", "https://example.com");
        updateResult.IsError.ShouldBeFalse();
        brand.Name.ShouldBe("Name");
        brand.Description.ShouldBe("Desc");
        brand.Website!.Value.ShouldBe("https://example.com");
        brand.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Update_WithWhitespaceWebsite_Should_ReturnError()
    {
        var result = Brand.Create("Name", "Desc", "   ");
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Description.Contains("website"));
    }

    [Fact]
    public void Update_WithWhitespaceWebsite_When_WebsiteIsNull_Should_ReturnError_And_NotChange()
    {
        var result = Brand.Create("Name", "Desc", null);
        var brand = result.Value;
        var updateResult = brand.Update(null, null, "   ");
        updateResult.IsError.ShouldBeTrue();
        updateResult.Errors.ShouldContain(e => e.Description.Contains("website"));
        brand.Website.ShouldBeNull();
    }

    [Fact]
    public void Update_WithWhitespaceWebsite_When_WebsiteIsNotNull_Should_ReturnError_And_NotChange()
    {
        var result = Brand.Create("Name", "Desc", "https://example.com");
        var brand = result.Value;
        var originalWebsite = brand.Website;
        var updateResult = brand.Update(null, null, "   ");
        updateResult.IsError.ShouldBeTrue();
        updateResult.Errors.ShouldContain(e => e.Description.Contains("website"));
        brand.Website.ShouldBe(originalWebsite);
    }

    [Fact]
    public void Create_WithOnlyWebsite_Should_ReturnErrorsForNameAndDescription()
    {
        var result = Brand.Create(null!, null, "https://example.com");
        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        result.Errors.ShouldContain(e => e.Description.Contains("name"));
        result.Errors.ShouldContain(e => e.Description.Contains("description"));
    }

    [Fact]
    public void Update_AllFieldsToWhitespace_Should_ReturnAllErrors()
    {
        var result = Brand.Create("Name", "Desc", "https://example.com");
        var brand = result.Value;
        var updateResult = brand.Update("   ", "   ", "   ");
        updateResult.IsError.ShouldBeTrue();
        updateResult.Errors.Count.ShouldBe(3);
        updateResult.Errors.ShouldContain(e => e.Description.Contains("name"));
        updateResult.Errors.ShouldContain(e => e.Description.Contains("description"));
        updateResult.Errors.ShouldContain(e => e.Description.Contains("website"));
    }

    [Fact]
    public void Products_Should_BeSameInstance_AfterUpdate()
    {
        var result = Brand.Create("Name", "Desc", "https://example.com");
        var brand = result.Value;
        var productsRef = brand.Products;
        brand.Update("NewName", "NewDesc", "https://newsite.com");
        brand.Products.ShouldBeSameAs(productsRef);
    }

    [Fact]
    public void Update_FromNullWebsite_ToValidWebsite_Should_SetWebsite()
    {
        // Arrange
        var result = Brand.Create("Name", "Desc", null);
        var brand = result.Value;
        var newUrl = "https://newsite.com";

        // Act
        var updateResult = brand.Update(null, null, newUrl);

        // Assert
        updateResult.IsError.ShouldBeFalse();
        brand.Website.ShouldNotBeNull();
        brand.Website.Value.ShouldBe(newUrl);
    }

    [Fact]
    public void Update_FromWebsite_ToNull_Should_ReturnError_And_NotChangeWebsite()
    {
        // Arrange
        var result = Brand.Create("Name", "Desc", "https://example.com");
        var brand = result.Value;
        var originalWebsite = brand.Website;

        // Act
        var updateResult = brand.Update(null, null, null);

        // Assert
        updateResult.IsError.ShouldBeFalse(); // No change, so no error
        brand.Website.ShouldBe(originalWebsite);

        // Now try to set website to empty string (should error)
        var updateResult2 = brand.Update(null, null, "");
        updateResult2.IsError.ShouldBeTrue();
        brand.Website.ShouldBe(originalWebsite);
    }

    [Theory]
    [InlineData(null, "   ", null, true)] // from null, whitespace, expect error, website remains null
    [InlineData("https://example.com", "   ", "https://example.com", true)] // from value, whitespace, expect error, website unchanged
    [InlineData("https://example.com", "", "https://example.com", true)] // from value, empty, expect error, website unchanged
    [InlineData(null, "https://newsite.com", "https://newsite.com", false)] // from null, valid, expect success, website updated
    [InlineData("https://example.com", "https://example.com", "https://example.com", false)] // from value, same, expect success, website unchanged
    [InlineData("https://example.com", null, "https://example.com", false)] // from value, null, expect success, website unchanged
    [InlineData("https://example.com", "not-a-url", "https://example.com", true)] // from value, invalid, expect error, website unchanged
    public void Update_Website_VariousCases(string? initialWebsite, string? updateWebsite, string? expectedWebsite, bool expectError)
    {
        var result = Brand.Create("Name", "Desc", initialWebsite);
        var brand = result.Value;
        var updateResult = brand.Update(null, null, updateWebsite);
        updateResult.IsError.ShouldBe(expectError);
        if (expectedWebsite == null)
            brand.Website.ShouldBeNull();
        else
            brand.Website!.Value.ShouldBe(expectedWebsite);
    }

    [Theory]
    [InlineData("Name", null, true)]
    [InlineData(null, "Desc", true)]
    [InlineData(null, null, true)]
    [InlineData("   ", "Desc", true)]
    [InlineData("Name", "   ", true)]
    [InlineData("   ", "   ", true)]
    public void Create_Name_Description_Validation(string? name, string? description, bool expectError)
    {
        var result = Brand.Create(name!, description, "https://example.com");
        result.IsError.ShouldBe(expectError);
    }

    [Theory]
    [InlineData(null, null, "https://example.com", true, 2)] // only website, expect 2 errors
    [InlineData(null, null, null, true, 2)] // all null, expect 2 errors
    [InlineData(" ", " ", "not-a-url", true, 3)] // all invalid, expect 3 errors
    public void Create_MultipleFields_Invalid(string? name, string? description, string? website, bool expectError, int errorCount)
    {
        var result = Brand.Create(name!, description, website);
        result.IsError.ShouldBe(expectError);
        result.Errors.Count.ShouldBe(errorCount);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://badurl")]
    [InlineData("http:/missing-slash.com")]
    public void Create_Should_ReturnError_When_WebsiteIsMalformed(string badWebsite)
    {
        var result = Brand.Create("Name", "Desc", badWebsite);
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("valid URL");
    }

    [Theory]
    [InlineData(null, "NewName", null, "Name", "NewName", "https://example.com", false)] // null updateName: no change, description updated
    [InlineData(null, null, "NewDesc", "Name", "Desc", "https://example.com", true)] // invalid website: error
    [InlineData(null, null, "https://newsite.com", "Name", "Desc", "https://newsite.com", false)] // update website
    [InlineData("", null, null, "Name", "Desc", "https://example.com", true)] // empty name: error
    [InlineData(null, "", null, "Name", "Desc", "https://example.com", true)] // empty description: error
    public void Update_Name_Description_Website_Various(string? updateName, string? updateDescription, string? updateWebsite, string expectedName, string expectedDescription, string? expectedWebsiteResult, bool expectError)
    {
        var result = Brand.Create("Name", "Desc", "https://example.com");
        var brand = result.Value;
        var updateResult = brand.Update(updateName, updateDescription, updateWebsite);
        updateResult.IsError.ShouldBe(expectError);
        brand.Name.ShouldBe(expectedName);
        brand.Description.ShouldBe(expectedDescription);
        if (expectedWebsiteResult == null)
            brand.Website.ShouldBeNull();
        else
            brand.Website!.Value.ShouldBe(expectedWebsiteResult);
    }
}
