using System.Reflection;
using Image.Generator.Application.Render.Features.SubmitRenderJob.V1;
using Shouldly;

namespace Image.Generator.UnitTests.Render;

public sealed class CacheKeyAndRequestTests
{
    private static string InvokeBuildCacheKey(SubmitRenderJobCommand command)
    {
        var method = typeof(SubmitRenderJobCommandHandler).GetMethod("BuildCacheKey", BindingFlags.NonPublic | BindingFlags.Static);
        return (string)method!.Invoke(null, new object[] { command })!;
    }

    private static SubmitRenderJobCommand CreateCommand(
        Guid? jobId = null,
        Guid? displayId = null,
        IDictionary<string, string>? data = null,
        ProviderProfile? providerProfile = null,
        SubmitRenderJobTemplateRequest? template = null)
    {
        return new SubmitRenderJobCommand(
            JobId: jobId ?? Guid.Empty,
            DisplayId: displayId ?? Guid.NewGuid(),
            TenantId: "tenant-1",
            OutputType: "png",
            PaletteColors: Array.Empty<string>(),
            Template: template ?? new SubmitRenderJobTemplateRequest
            {
                Width = 100,
                Height = 50,
                BackgroundColor = "#FFFFFF",
                Elements =
                [
                    new SubmitRenderJobTemplateElementRequest
                    {
                        Type = "text",
                        Left = 0,
                        Top = 0,
                        Width = 100,
                        Height = 20,
                        Value = "Test",
                        Binding = string.Empty,
                        Format = string.Empty,
                        FontFamily = "Arial",
                        FontSize = 12,
                        FontWeight = "normal",
                        HorizontalAlign = "left",
                        ForegroundColor = "#000000",
                        BackgroundColor = string.Empty,
                        StrokeWidth = 0,
                        CornerRadius = 0,
                        Fill = false,
                        WordWrap = false,
                        MaxLines = 0,
                        LineHeight = 1.2f,
                        Ellipsis = "…",
                        AutoSize = false,
                        MinFontSize = 8,
                        MaxFontSize = 72,
                        TextEffect = "none",
                        ShowValue = false,
                        X1 = 0,
                        Y1 = 0,
                        X2 = 0,
                        Y2 = 0,
                        Padding = string.Empty,
                        BadgeStyle = string.Empty,
                        WasPrice = string.Empty,
                        NowPrice = string.Empty,
                        Currency = string.Empty,
                        TextDirection = "auto",
                        GradientType = "none",
                        GradientAngle = 0,
                        GradientStartX = 0,
                        GradientStartY = 0,
                        GradientEndX = 0,
                        GradientEndY = 0,
                        ElementId = string.Empty,
                    }
                ],
            },
            Data: (IReadOnlyDictionary<string, string>?)data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ProviderProfile: providerProfile);
    }

    [Fact]
    public void BuildCacheKey_WithIdenticalTemplateAndData_ShouldProduceSameKey()
    {
        var cmd1 = CreateCommand(jobId: Guid.NewGuid(), displayId: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var cmd2 = CreateCommand(jobId: Guid.NewGuid(), displayId: Guid.Parse("00000000-0000-0000-0000-000000000001"));

        string key1 = InvokeBuildCacheKey(cmd1);
        string key2 = InvokeBuildCacheKey(cmd2);

        key1.ShouldBe(key2);
    }

    [Fact]
    public void BuildCacheKey_WithDifferentDisplayId_ShouldProduceDifferentKey()
    {
        var cmd1 = CreateCommand(displayId: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var cmd2 = CreateCommand(displayId: Guid.Parse("00000000-0000-0000-0000-000000000002"));

        string key1 = InvokeBuildCacheKey(cmd1);
        string key2 = InvokeBuildCacheKey(cmd2);

        key1.ShouldNotBe(key2);
    }

    [Fact]
    public void BuildCacheKey_WithDifferentJobId_ShouldProduceSameKey()
    {
        var displayId = Guid.Parse("00000000-0000-0000-0000-0000000000AA");
        var cmd1 = CreateCommand(jobId: Guid.NewGuid(), displayId: displayId);
        var cmd2 = CreateCommand(jobId: Guid.NewGuid(), displayId: displayId);

        string key1 = InvokeBuildCacheKey(cmd1);
        string key2 = InvokeBuildCacheKey(cmd2);

        key1.ShouldBe(key2);
    }

    [Fact]
    public void BuildCacheKey_WithDifferentData_ShouldProduceDifferentKey()
    {
        var data1 = new Dictionary<string, string> { ["a"] = "1" };
        var data2 = new Dictionary<string, string> { ["a"] = "2" };

        var cmd1 = CreateCommand(data: data1);
        var cmd2 = CreateCommand(data: data2);

        InvokeBuildCacheKey(cmd1).ShouldNotBe(InvokeBuildCacheKey(cmd2));
    }

    [Fact]
    public void BuildCacheKey_WithDifferentTemplate_ShouldProduceDifferentKey()
    {
        var t1 = new SubmitRenderJobTemplateRequest { Width = 100, Height = 50 };
        var t2 = new SubmitRenderJobTemplateRequest { Width = 200, Height = 50 };

        var cmd1 = CreateCommand(template: t1);
        var cmd2 = CreateCommand(template: t2);

        InvokeBuildCacheKey(cmd1).ShouldNotBe(InvokeBuildCacheKey(cmd2));
    }

    [Fact]
    public void BuildCacheKey_WithProviderProfile_PresenceAndNameAffectsKey()
    {
        var profileA = new ProviderProfile { ProviderName = "one", ScreenWidth = 1, ScreenHeight = 1 };
        var profileB = new ProviderProfile { ProviderName = "two", ScreenWidth = 1, ScreenHeight = 1 };

        var displayId = Guid.Parse("00000000-0000-0000-0000-0000000000BB");
        var noProfile = CreateCommand(displayId: displayId, providerProfile: null);
        var withA = CreateCommand(displayId: displayId, providerProfile: profileA);
        var withB = CreateCommand(displayId: displayId, providerProfile: profileB);

        InvokeBuildCacheKey(noProfile).ShouldNotBe(InvokeBuildCacheKey(withA));
        InvokeBuildCacheKey(withA).ShouldNotBe(InvokeBuildCacheKey(withB));
        InvokeBuildCacheKey(withA).ShouldBe(InvokeBuildCacheKey(CreateCommand(displayId: displayId, providerProfile: profileA)));
    }

    [Fact]
    public void BuildCacheKey_Phase5Properties_TextDirectionGradientElementIdAffectKey()
    {
        var baseCmd = CreateCommand();

        var e1 = baseCmd.Template.Elements[0] with { TextDirection = "ltr" };
        var e2 = baseCmd.Template.Elements[0] with { TextDirection = "rtl" };

        var cmd1 = CreateCommand(template: baseCmd.Template with { Elements = [ e1 ] });
        var cmd2 = CreateCommand(template: baseCmd.Template with { Elements = [ e2 ] });
        InvokeBuildCacheKey(cmd1).ShouldNotBe(InvokeBuildCacheKey(cmd2));

        var g1 = baseCmd.Template.Elements[0] with { GradientType = "none" };
        var g2 = baseCmd.Template.Elements[0] with { GradientType = "linear" };
        var cmd3 = CreateCommand(template: baseCmd.Template with { Elements = [ g1 ] });
        var cmd4 = CreateCommand(template: baseCmd.Template with { Elements = [ g2 ] });
        InvokeBuildCacheKey(cmd3).ShouldNotBe(InvokeBuildCacheKey(cmd4));

        var id1 = baseCmd.Template.Elements[0] with { ElementId = "a" };
        var id2 = baseCmd.Template.Elements[0] with { ElementId = "b" };
        var cmd5 = CreateCommand(template: baseCmd.Template with { Elements = [ id1 ] });
        var cmd6 = CreateCommand(template: baseCmd.Template with { Elements = [ id2 ] });
        InvokeBuildCacheKey(cmd5).ShouldNotBe(InvokeBuildCacheKey(cmd6));
    }

    [Fact]
    public void RequestModel_Defaults_AreAsExpected()
    {
        var request = new SubmitRenderJobRequest();
        request.OutputType.ShouldBe("png");

        var template = new SubmitRenderJobTemplateRequest();
        template.Width.ShouldBe(1200);

        var profile = new ProviderProfile();
        profile.ColorDepth.ShouldBe(1);
        profile.QuantizeToPalette.ShouldBeTrue();
    }

    [Fact]
    public void BuildCacheKey_IncludesElementProperties_KeyNotEmptyAndHasPrefix()
    {
        var cmd = CreateCommand();
        string key = InvokeBuildCacheKey(cmd);
        key.ShouldNotBeNullOrWhiteSpace();
        key.ShouldStartWith("image-generator:render:");
    }
}
