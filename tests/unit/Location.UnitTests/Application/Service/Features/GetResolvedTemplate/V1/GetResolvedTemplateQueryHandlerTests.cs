using Location.Application.Service.Abstractions;
using Location.Application.Service.Features.GetResolvedTemplate.V1;
using NSubstitute;
using Shouldly;

namespace Location.UnitTests.Application.Service.Features.GetResolvedTemplate.V1;

public sealed class GetResolvedTemplateQueryHandlerTests
{
    private readonly ITemplateInheritanceResolver resolver = Substitute.For<ITemplateInheritanceResolver>();

    [Fact]
    public async Task Handle_WhenResolverReturnsContext_ShouldMapResponse()
    {
        GetResolvedTemplateQuery request = new("loc-1", "tmpl-explicit");
        ResolvedTemplateContext context = new()
        {
            LocationNodeId = "loc-1",
            ResolvedTemplateId = "tmpl-explicit",
            TemplateSource = "Request",
            ResolvedTemplateDesign = new TemplateDesignSnapshot("tenant-1", "tmpl-explicit", "Main", 100, 200, "#FFFFFF", "[]", "{}"),
            EffectiveSettings = new Dictionary<string, EffectiveSettingValue>
            {
                ["logo"] = new("logo", "logo.png", "Tenant", "_tenant", "inherit"),
            },
            InheritanceChain =
            [
                new InheritanceSource("Tenant", "_tenant", "Tenant", new Dictionary<string, ScopedSetting>
                {
                    ["logo"] = new("inherit", "logo.png"),
                }),
            ],
        };

        this.resolver.ResolveAsync("_current", "loc-1", "tmpl-explicit", Arg.Any<CancellationToken>())
            .Returns(context);

        GetResolvedTemplateQueryHandler sut = new(this.resolver);

        var result = await sut.Handle(request, TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        result.Value.LocationNodeId.ShouldBe("loc-1");
        result.Value.ResolvedTemplateId.ShouldBe("tmpl-explicit");
        result.Value.TemplateSource.ShouldBe("Request");
        result.Value.TemplateDesign.ShouldNotBeNull();
        result.Value.TemplateDesign.TemplateId.ShouldBe("tmpl-explicit");
        result.Value.EffectiveSettings.ShouldContainKey("logo");
        result.Value.InheritanceChain.Count.ShouldBe(1);
    }
}
