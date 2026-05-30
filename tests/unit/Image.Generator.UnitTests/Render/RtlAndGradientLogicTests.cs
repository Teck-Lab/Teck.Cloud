using Image.Generator.Application.Render.Features.SubmitRenderJob.V1;
using Shouldly;
using SkiaSharp;

namespace Image.Generator.UnitTests.Render;

public sealed class RtlAndGradientLogicTests
{
    private static readonly CompiledElement DefaultElement = new()
    {
        NormalizedType = "text",
        Left = 0, Top = 0, Width = 100, Height = 100,
        ForegroundColor = SKColors.Black,
        BackgroundColor = SKColors.White,
        StrokeWidth = 0, CornerRadius = 0, Fill = false,
        FontFamily = "Arial", FontSize = 16, FontWeight = "normal",
        HorizontalAlign = "left", WordWrap = false, MaxLines = 0,
        LineHeight = 1.2f, Ellipsis = "…", AutoSize = false,
        MinFontSize = 8, MaxFontSize = 72, TextEffect = "none",
        TextDirection = "auto", GradientType = "none",
        GradientColors = [], GradientAngle = 0,
        GradientStartX = 0, GradientStartY = 0,
        GradientEndX = 0, GradientEndY = 0,
        ElementId = "", ShowValue = false,
        X1 = 0, Y1 = 0, X2 = 0, Y2 = 0,
        Children = [], Padding = "", BadgeStyle = "",
        WasPrice = "", NowPrice = "", Currency = "",
        Value = "", Binding = "", Format = "",
    };

    private static T? InvokePrivateStatic<T>(string methodName, params object[] args)
    {
        var method = typeof(SkiaRenderJobRenderer).GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (T?)method?.Invoke(null, args);
    }

    [Theory]
    [InlineData("שלום", true)] // Hebrew
    [InlineData("مرحبا", true)] // Arabic
    [InlineData("Hello", false)]
    [InlineData("", false)]
    [InlineData("Hello שלום", true)] // Mixed
    public void IsRightToLeftText_WithVariousInputs_ReturnsExpected(string text, bool expected)
    {
        bool result = InvokePrivateStatic<bool>("IsRightToLeftText", text);
        result.ShouldBe(expected);
    }

    [Fact]
    public void IsRightToLeftText_WithNull_ReturnsFalse()
    {
        bool result = InvokePrivateStatic<bool>("IsRightToLeftText", new object[] { null! });
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("שלום", "auto", "rtl")]
    [InlineData("Hello", "auto", "ltr")]
    [InlineData("irrelevant", "rtl", "rtl")]
    [InlineData("irrelevant", "ltr", "ltr")]
    [InlineData("שלום", "AUTO", "rtl")]
    public void ResolveTextDirection_VariousInputs_ReturnsExpected(string text, string direction, string expected)
    {
        string result = InvokePrivateStatic<string>("ResolveTextDirection", text, direction)!;
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(SKTextAlign.Left, SKTextAlign.Right)]
    [InlineData(SKTextAlign.Right, SKTextAlign.Left)]
    [InlineData(SKTextAlign.Center, SKTextAlign.Center)]
    public void SwapAlignment_WhenCalled_ReturnsSwapped(SKTextAlign input, SKTextAlign expected)
    {
        SKTextAlign result = InvokePrivateStatic<SKTextAlign>("SwapAlignment", input);
        result.ShouldBe(expected);
    }

    [Fact]
    public void SwapAnchorX_LeftAlignedRtl_FlipsToRightEdge()
    {
        var element = DefaultElement with
        {
            Left = 10,
            Width = 50,
        };

        float originalAnchor = 10f;
        float result = InvokePrivateStatic<float>("SwapAnchorX", originalAnchor, SKTextAlign.Left, element);
        result.ShouldBe(element.Left + element.Width);
    }

    [Fact]
    public void SwapAnchorX_RightAlignedRtl_FlipsToLeftEdge()
    {
        var element = DefaultElement with
        {
            Left = 5,
            Width = 20,
        };

        float originalAnchor = element.Left + element.Width;
        float result = InvokePrivateStatic<float>("SwapAnchorX", originalAnchor, SKTextAlign.Right, element);
        result.ShouldBe(element.Left);
    }

    [Fact]
    public void SwapAnchorX_CenterUnchanged()
    {
        var element = DefaultElement with
        {
            Left = 0,
            Width = 100,
        };

        float originalAnchor = element.Left + (element.Width / 2f);
        float result = InvokePrivateStatic<float>("SwapAnchorX", originalAnchor, SKTextAlign.Center, element);
        result.ShouldBe(originalAnchor);
    }

    [Fact]
    public void SwapAnchorX_ZeroWidth_ReturnsOriginalAnchor()
    {
        var element = DefaultElement with
        {
            Left = 15,
            Width = 0,
        };

        float originalAnchor = 15f;
        float result = InvokePrivateStatic<float>("SwapAnchorX", originalAnchor, SKTextAlign.Left, element);
        result.ShouldBe(originalAnchor);
    }

    [Fact]
    public void CreateGradientShader_NoneOrLessThanTwoColors_ReturnsNull()
    {
        var element = DefaultElement with
        {
            GradientType = "none",
            GradientColors = new[] { SKColors.Red },
            Left = 0, Top = 0, Width = 10, Height = 10
        };

        SKRect bounds = new(0, 0, 10, 10);
        var result = InvokePrivateStatic<SKShader>("CreateGradientShader", element, bounds);
        result.ShouldBeNull();
    }

    [Fact]
    public void CreateGradientShader_LinearAngle_ReturnsShader()
    {
        var element = DefaultElement with
        {
            GradientType = "linear",
            GradientColors = new[] { SKColors.Red, SKColors.Blue },
            GradientAngle = 45,
            Left = 0, Top = 0, Width = 100, Height = 50
        };

        SKRect bounds = new(0, 0, 100, 50);
        var result = InvokePrivateStatic<SKShader>("CreateGradientShader", element, bounds);
        result.ShouldNotBeNull();
    }

    [Fact]
    public void CreateGradientShader_LinearExplicitStartEnd_ReturnsShader()
    {
        var element = DefaultElement with
        {
            GradientType = "linear",
            GradientColors = new[] { SKColors.Red, SKColors.Green, SKColors.Blue },
            GradientStartX = 0f,
            GradientStartY = 0f,
            GradientEndX = 1f,
            GradientEndY = 1f,
            Left = 0, Top = 0, Width = 200, Height = 200
        };

        SKRect bounds = new(0, 0, 200, 200);
        var result = InvokePrivateStatic<SKShader>("CreateGradientShader", element, bounds);
        result.ShouldNotBeNull();
    }

    [Fact]
    public void CreateGradientShader_Radial_ReturnsShader()
    {
        var element = DefaultElement with
        {
            GradientType = "radial",
            GradientColors = new[] { SKColors.Yellow, SKColors.Orange },
            Left = 0, Top = 0, Width = 80, Height = 120
        };

        SKRect bounds = new(0, 0, 80, 120);
        var result = InvokePrivateStatic<SKShader>("CreateGradientShader", element, bounds);
        result.ShouldNotBeNull();
    }
}
