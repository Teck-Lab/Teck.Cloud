#nullable enable
using Catalog.Domain.Entities.BrandAggregate.Errors;
using Catalog.Domain.Entities.BrandAggregate.ValueObjects;
using Shouldly;

namespace Catalog.UnitTests.Domain.Brands;

public class WebsiteTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_ReturnEmptyError_When_NullOrWhitespace(string? value)
    {
        var result = Website.Create(value);
        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(WebsiteErrors.Empty);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    [InlineData("http:/missing-slash.com")]
    [InlineData("www.example.com")]
    public void Create_Should_ReturnInvalidError_When_NotValidUrl(string value)
    {
        var result = Website.Create(value);
        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(WebsiteErrors.Invalid);
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com")]
    [InlineData("HTTPS://EXAMPLE.COM")]
    public void Create_Should_Succeed_When_ValidUrl(string value)
    {
        var result = Website.Create(value);
        result.IsError.ShouldBeFalse();
        result.Value.Value.ShouldBe(value);
    }

    [Fact]
    public void ToString_Should_ReturnValue()
    {
        var url = "https://example.com";
        var website = Website.Create(url).Value;
        website.ToString().ShouldBe(url);
    }

    [Fact]
    public void Equality_Should_Work_For_SameValue()
    {
        var url = "https://example.com";
        var w1 = Website.Create(url).Value;
        var w2 = Website.Create(url).Value;
        w1.ShouldBe(w2);
        w1.GetHashCode().ShouldBe(w2.GetHashCode());
    }

    [Fact]
    public void Equality_Should_Fail_For_DifferentValue()
    {
        var w1 = Website.Create("https://a.com").Value;
        var w2 = Website.Create("https://b.com").Value;
        w1.ShouldNotBe(w2);
    }

    [Fact]
    public void WebsiteErrors_Empty_Should_Have_Correct_Code_And_Description()
    {
        WebsiteErrors.Empty.Code.ShouldBe("Website.Empty");
        WebsiteErrors.Empty.Description.ShouldContain("empty");
    }

    [Fact]
    public void WebsiteErrors_Invalid_Should_Have_Correct_Code_And_Description()
    {
        WebsiteErrors.Invalid.Code.ShouldBe("Website.Invalid");
        WebsiteErrors.Invalid.Description.ShouldContain("valid URL");
    }
}
