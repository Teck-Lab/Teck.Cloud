// <copyright file="TemplateCompilerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Shouldly;
using SkiaSharp;
using Image.Generator.Application.Render.Features.SubmitRenderJob.V1;

namespace Image.Generator.UnitTests.Render;

public sealed class TemplateCompilerTests
{
    [Fact]
    public void Compile_WhenSimpleTemplate_ShouldPreserveBasicProperties()
    {
        var template = new SubmitRenderJobTemplateRequest
        {
            Width = 200,
            Height = 100,
            BackgroundColor = "#112233",
            UsePercentagePositioning = true,
            Elements =
            [
                new SubmitRenderJobTemplateElementRequest
                {
                    Type = " Text ",
                    Left = 10,
                    Top = 20,
                    Width = 50,
                    Height = 12,
                    ForegroundColor = "#010203",
                    BackgroundColor = "#040506",
                    StrokeWidth = 1.5f,
                    CornerRadius = 2f,
                    Fill = true,
                    FontFamily = "Verdana",
                    FontSize = 14,
                    FontWeight = "Bold",
                    HorizontalAlign = "center",
                    WordWrap = true,
                    MaxLines = 2,
                    LineHeight = 1.5f,
                    Ellipsis = "...",
                    AutoSize = false,
                    MinFontSize = 6,
                    MaxFontSize = 20,
                    TextEffect = "Shadow",
                    TextDirection = "ltr",
                    GradientType = "linear",
                    GradientColors = ["#000000", "#FFFFFF"],
                    GradientAngle = 45,
                    GradientStartX = 0.1f,
                    GradientStartY = 0.2f,
                    GradientEndX = 0.3f,
                    GradientEndY = 0.4f,
                    ElementId = "e1",
                    ShowValue = true,
                    X1 = 1,
                    Y1 = 2,
                    X2 = 3,
                    Y2 = 4,
                    Padding = "1,2,3,4",
                    BadgeStyle = "pill",
                    WasPrice = "5.00",
                    NowPrice = "3.00",
                    Currency = "$",
                    Value = "hello",
                    Binding = "price",
                    Format = "N2",
                }
            ],
        };

        CompiledTemplate compiled = TemplateCompiler.Compile(template);

        compiled.Width.ShouldBe(200);
        compiled.Height.ShouldBe(100);
        compiled.BackgroundColor.ShouldBe(new SKColor(0x11, 0x22, 0x33, 255));
        compiled.UsePercentagePositioning.ShouldBeTrue();
        compiled.Elements.Count.ShouldBe(1);

        CompiledElement e = compiled.Elements[0];
        e.NormalizedType.ShouldBe("text");
        e.Left.ShouldBe(10);
        e.Top.ShouldBe(20);
        e.Width.ShouldBe(50);
        e.Height.ShouldBe(12);
        e.ForegroundColor.ShouldBe(new SKColor(0x01, 0x02, 0x03, 255));
        e.BackgroundColor.ShouldBe(new SKColor(0x04, 0x05, 0x06, 255));
        e.StrokeWidth.ShouldBe(1.5f);
        e.CornerRadius.ShouldBe(2f);
        e.Fill.ShouldBeTrue();
        e.FontFamily.ShouldBe("Verdana");
        e.FontSize.ShouldBe(14);
        e.FontWeight.ShouldBe("Bold");
        e.HorizontalAlign.ShouldBe("center");
        e.WordWrap.ShouldBeTrue();
        e.MaxLines.ShouldBe(2);
        e.LineHeight.ShouldBe(1.5f);
        e.Ellipsis.ShouldBe("...");
        e.AutoSize.ShouldBeFalse();
        e.MinFontSize.ShouldBe(6);
        e.MaxFontSize.ShouldBe(20);
        e.TextEffect.ShouldBe("shadow");
        e.TextDirection.ShouldBe("ltr");
        e.GradientType.ShouldBe("linear");
        e.GradientColors.Count.ShouldBe(2);
        e.GradientAngle.ShouldBe(45);
        e.GradientStartX.ShouldBe(0.1f);
        e.GradientStartY.ShouldBe(0.2f);
        e.GradientEndX.ShouldBe(0.3f);
        e.GradientEndY.ShouldBe(0.4f);
        e.ElementId.ShouldBe("e1");
        e.ShowValue.ShouldBeTrue();
        e.X1.ShouldBe(1);
        e.Y1.ShouldBe(2);
        e.X2.ShouldBe(3);
        e.Y2.ShouldBe(4);
        e.Padding.ShouldBe("1,2,3,4");
        e.BadgeStyle.ShouldBe("pill");
        e.WasPrice.ShouldBe("5.00");
        e.NowPrice.ShouldBe("3.00");
        e.Currency.ShouldBe("$");
        e.Value.ShouldBe("hello");
        e.Binding.ShouldBe("price");
        e.Format.ShouldBe("N2");
    }

    [Fact]
    public void MergeElements_WhenChildOverridesParent_ShouldReplaceAndAppend()
    {
        var parent = new SubmitRenderJobTemplateRequest
        {
            Elements =
            [
                new SubmitRenderJobTemplateElementRequest { ElementId = "a", Type = "text", Left = 1 },
                new SubmitRenderJobTemplateElementRequest { ElementId = "b", Type = "text", Left = 2 },
            ]
        };

        var child = new SubmitRenderJobTemplateRequest
        {
            Elements =
            [
                new SubmitRenderJobTemplateElementRequest { ElementId = "b", Type = "text", Left = 20 },
                new SubmitRenderJobTemplateElementRequest { ElementId = "c", Type = "text", Left = 30 },
            ]
        };

        var merged = typeof(TemplateCompiler)
            .GetMethod("MergeElements", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { parent.Elements, child.Elements }) as IReadOnlyList<SubmitRenderJobTemplateElementRequest>;

        merged.ShouldNotBeNull();
        merged!.Count.ShouldBe(3);
        merged.Any(x => x.ElementId == "a" && x.Left == 1).ShouldBeTrue();
        merged.Any(x => x.ElementId == "b" && x.Left == 20).ShouldBeTrue();
        merged.Any(x => x.ElementId == "c" && x.Left == 30).ShouldBeTrue();
    }

    [Fact]
    public void MergeElements_WhenParentEmpty_ShouldReturnChildOnly()
    {
        var parent = new SubmitRenderJobTemplateRequest { Elements = [] };
        var child = new SubmitRenderJobTemplateRequest
        {
            Elements = [ new SubmitRenderJobTemplateElementRequest { ElementId = "x" } ]
        };

        var merged = typeof(TemplateCompiler)
            .GetMethod("MergeElements", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { parent.Elements, child.Elements }) as IReadOnlyList<SubmitRenderJobTemplateElementRequest>;

        merged.ShouldNotBeNull();
        merged!.Count.ShouldBe(1);
        merged[0].ElementId.ShouldBe("x");
    }

    [Fact]
    public void MergeElements_WhenChildEmpty_ShouldReturnParentOnly()
    {
        var parent = new SubmitRenderJobTemplateRequest
        {
            Elements = [ new SubmitRenderJobTemplateElementRequest { ElementId = "p" } ]
        };
        var child = new SubmitRenderJobTemplateRequest { Elements = [] };

        var merged = typeof(TemplateCompiler)
            .GetMethod("MergeElements", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { parent.Elements, child.Elements }) as IReadOnlyList<SubmitRenderJobTemplateElementRequest>;

        merged.ShouldNotBeNull();
        merged!.Count.ShouldBe(1);
        merged[0].ElementId.ShouldBe("p");
    }

    [Fact]
    public void MergeElement_FieldPrecedence_WorksAsExpected()
    {
        var parent = new SubmitRenderJobTemplateElementRequest
        {
            ElementId = "id",
            Type = "parentType",
            Left = 1,
            Top = 2,
            Width = 3,
            Height = 4,
            Value = "pval",
            Binding = "pbind",
            FontSize = 10,
            Fill = false,
            WordWrap = false,
            AutoSize = false,
            GradientColors = ["#000000"],
        };

        var child = new SubmitRenderJobTemplateElementRequest
        {
            ElementId = "id",
            Type = string.Empty,
            Left = 0,
            Top = 20,
            Width = 0,
            Height = 40,
            Value = string.Empty,
            Binding = "cbind",
            FontSize = 0,
            Fill = true,
            WordWrap = true,
            AutoSize = true,
            GradientColors = [],
        };

        var merged = typeof(TemplateCompiler)
            .GetMethod("MergeElement", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { parent, child }) as SubmitRenderJobTemplateElementRequest;

        merged.ShouldNotBeNull();
        merged!.Type.ShouldBe("parentType");
        merged.Left.ShouldBe(1);
        merged.Top.ShouldBe(20);
        merged.Width.ShouldBe(3);
        merged.Height.ShouldBe(40);
        merged.Value.ShouldBe("pval");
        merged.Binding.ShouldBe("cbind");
        merged.FontSize.ShouldBe(10);
        merged.Fill.ShouldBeTrue();
        merged.WordWrap.ShouldBeTrue();
        merged.AutoSize.ShouldBeTrue();
        merged.GradientColors.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("#112233", 0x11, 0x22, 0x33, 255)]
    [InlineData("#80112233", 0x11, 0x22, 0x33, 0x80)]
    public void ParseColor_ValidFormats_ShouldParseCorrectly(string input, byte r, byte g, byte b, byte a)
    {
        var method = typeof(TemplateCompiler)
            .GetMethod("ParseColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        SKColor result = (SKColor)method.Invoke(null, new object[] { input, SKColors.Magenta })!;

        result.Red.ShouldBe(r);
        result.Green.ShouldBe(g);
        result.Blue.ShouldBe(b);
        result.Alpha.ShouldBe(a);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    public void ParseColor_InvalidOrEmpty_ShouldReturnFallback(string input)
    {
        var method = typeof(TemplateCompiler)
            .GetMethod("ParseColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        SKColor result = (SKColor)method.Invoke(null, new object[] { input, SKColors.Coral })!;

        result.ShouldBe(SKColors.Coral);
    }
}
