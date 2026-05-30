using Image.Generator.Application.Render.Features.SubmitRenderJob.V1;
using Image.Generator.Application.Render.Features.SubmitRenderJob.V1.ProviderEncoders;
using Shouldly;
using SkiaSharp;

namespace Image.Generator.UnitTests.Render;

public sealed class ProviderEncoderTests
{
    private static SKImage CreateTestImage(int width, int height, SKColor color)
    {
        var surface = SKSurface.Create(new SKImageInfo(width, height));
        try
        {
            surface.Canvas.Clear(color);
            return surface.Snapshot();
        }
        finally
        {
            surface.Dispose();
        }
    }

    [Fact]
    public void HanshowBinaryEncoder_ProviderName_ShouldBeHanshow()
    {
        var encoder = new HanshowBinaryEncoder();
        encoder.ProviderName.ShouldBe("hanshow");
    }

    [Fact]
    public void HanshowBinaryEncoder_WhenBlackWhiteImage_Produces1bppSize()
    {
        using SKImage img = CreateTestImage(10, 10, SKColors.Black);
        var encoder = new HanshowBinaryEncoder();
        var profile = new ProviderProfile { SupportedColors = Array.Empty<string>() };

        byte[] output = encoder.Encode(img, profile);

        int bytesPerRow = (int)Math.Ceiling(10 * 1 / 8.0);
        int expectedSize = bytesPerRow * 10;
        output.Length.ShouldBe(expectedSize);
    }

    [Fact]
    public void HanshowBinaryEncoder_WhenBlackWhiteRedImage_Produces2bppSize()
    {
        using SKImage img = CreateTestImage(10, 10, SKColors.Red);
        var encoder = new HanshowBinaryEncoder();
        var profile = new ProviderProfile { SupportedColors = new[] { "red" } };

        byte[] output = encoder.Encode(img, profile);

        int bytesPerRow = (int)Math.Ceiling(10 * 2 / 8.0);
        int expectedSize = bytesPerRow * 10;
        output.Length.ShouldBe(expectedSize);
    }

    [Fact]
    public void PricerPpmEncoder_ProviderName_ShouldBePricer()
    {
        var encoder = new PricerPpmEncoder();
        encoder.ProviderName.ShouldBe("pricer");
    }

    [Fact]
    public void PricerPpmEncoder_WhenRendered_OutputIsValidPpm()
    {
        using SKImage img = CreateTestImage(4, 2, SKColors.Red);
        var encoder = new PricerPpmEncoder();
        var profile = new ProviderProfile { SupportedColors = new[] { "red" } };

        byte[] output = encoder.Encode(img, profile);
        string text = System.Text.Encoding.ASCII.GetString(output);

        text.ShouldStartWith("P3\n");
        text.ShouldContain("4 2");
        text.ShouldContain("255\n");
        // Verify that pixels are quantized to expected palette values (255 0 0 for red)
        text.ShouldContain("255 0 0");
    }

    [Fact]
    public void SesPngEncoder_ProviderName_ShouldBeSes()
    {
        var encoder = new SesPngEncoder();
        encoder.ProviderName.ShouldBe("ses");
    }

    [Fact]
    public void SesPngEncoder_WhenRendered_ProducesPngBytes()
    {
        using SKImage img = CreateTestImage(8, 8, SKColors.White);
        var encoder = new SesPngEncoder();
        var profile = new ProviderProfile { SupportedColors = Array.Empty<string>() };

        byte[] output = encoder.Encode(img, profile);
        output.Length.ShouldBeGreaterThan(8);
        output[0].ShouldBe((byte)0x89);
        output[1].ShouldBe((byte)0x50);
        output[2].ShouldBe((byte)0x4E);
        output[3].ShouldBe((byte)0x47);
    }

    [Fact]
    public void SolumBmpEncoder_ProviderName_ShouldBeSolum()
    {
        var encoder = new SolumBmpEncoder();
        encoder.ProviderName.ShouldBe("solum");
    }

    [Fact]
    public void SolumBmpEncoder_WhenRendered_ProducesBmpHeaderAndPalette_Bw()
    {
        using SKImage img = CreateTestImage(5, 3, SKColors.Black);
        var encoder = new SolumBmpEncoder();
        var profile = new ProviderProfile { SupportedColors = Array.Empty<string>() };

        byte[] output = encoder.Encode(img, profile);

        // BM
        output[0].ShouldBe((byte)'B');
        output[1].ShouldBe((byte)'M');

        // DIB header size at offset 14 (little endian int)
        int dibSize = BitConverter.ToInt32(output, 14);
        dibSize.ShouldBe(40);

        // Palette should have 2 colors (black + white) -> written starting at offset 54
        // Each palette entry is 4 bytes; check first two entries are present
        output.Length.ShouldBeGreaterThanOrEqualTo(54 + 8);
        // Black entry
        output[54].ShouldBe((byte)0);
        output[55].ShouldBe((byte)0);
        output[56].ShouldBe((byte)0);
        // White entry
        output[58].ShouldBe((byte)255);
        output[59].ShouldBe((byte)255);
        output[60].ShouldBe((byte)255);
    }

    [Fact]
    public void SolumBmpEncoder_WhenRendered_ProducesBmpHeaderAndPalette_Red()
    {
        using SKImage img = CreateTestImage(6, 4, SKColors.Red);
        var encoder = new SolumBmpEncoder();
        var profile = new ProviderProfile { SupportedColors = new[] { "red" } };

        byte[] output = encoder.Encode(img, profile);

        output[0].ShouldBe((byte)'B');
        output[1].ShouldBe((byte)'M');

        int dibSize = BitConverter.ToInt32(output, 14);
        dibSize.ShouldBe(40);

        // Palette should have 4 colors (4*4=16 bytes) starting at 54
        output.Length.ShouldBeGreaterThanOrEqualTo(54 + 16);
        // Check red entry exists (third entry, offset + 8)
        int redOffset = 54 + 8;
        // In SolumBmpEncoder they wrote colors as {0,0,0,0},{255,255,255,0},{0,0,255,0},{0,255,255,0}
        // So red is at index 2 -> bytes at redOffset should be 0,0,255
        output[redOffset].ShouldBe((byte)0);
        output[redOffset + 1].ShouldBe((byte)0);
        output[redOffset + 2].ShouldBe((byte)255);
    }
}
