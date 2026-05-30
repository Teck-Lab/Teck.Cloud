// <copyright file="SesPngEncoder.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;
using SkiaSharp;

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1.ProviderEncoders;

/// <summary>
/// Encodes images for SES-imagotag ESL displays.
/// Outputs PNG with palette-optimized colors (passthrough with quantization).
/// </summary>
internal sealed class SesPngEncoder : IProviderEncoder
{
    public string ProviderName => "ses";

    public byte[] Encode(SKImage image, ProviderProfile profile)
    {
        // SES supports PNG natively, but we should quantize to supported colors
        bool hasRed = profile.SupportedColors.Contains("red", StringComparer.OrdinalIgnoreCase);
        bool hasYellow = profile.SupportedColors.Contains("yellow", StringComparer.OrdinalIgnoreCase);

        int width = image.Width;
        int height = image.Height;

        // Create quantized bitmap
        using SKBitmap quantized = new(new SKImageInfo(width, height, SKColorType.Rgba8888));

        SKImageInfo info = new(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        byte[] pixels = new byte[width * height * 4];
        GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            image.ReadPixels(info, handle.AddrOfPinnedObject(), width * 4, 0, 0);
        }
        finally
        {
            handle.Free();
        }

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int pixelIndex = ((row * width) + column) * 4;
                byte red = pixels[pixelIndex];
                byte green = pixels[pixelIndex + 1];
                byte blue = pixels[pixelIndex + 2];

                SKColor nearest = FindNearestColor(red, green, blue, hasRed, hasYellow);
                quantized.SetPixel(column, row, nearest);
            }
        }

        using SKData data = quantized.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static SKColor FindNearestColor(byte red, byte green, byte blue, bool hasRed, bool hasYellow)
    {
        SKColor[] palette;
        if (hasRed)
        {
            if (hasYellow)
            {
                palette = [new SKColor(0, 0, 0), new SKColor(255, 255, 255), new SKColor(255, 0, 0), new SKColor(255, 255, 0)];
            }
            else
            {
                palette = [new SKColor(0, 0, 0), new SKColor(255, 255, 255), new SKColor(255, 0, 0)];
            }
        }
        else
        {
            palette = [new SKColor(0, 0, 0), new SKColor(255, 255, 255)];
        }

        SKColor nearest = palette[0];
        int nearestDist = int.MaxValue;

        foreach (SKColor color in palette)
        {
            int deltaRed = red - color.Red;
            int deltaGreen = green - color.Green;
            int deltaBlue = blue - color.Blue;
            int dist = (deltaRed * deltaRed) + (deltaGreen * deltaGreen) + (deltaBlue * deltaBlue);

            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = color;
            }
        }

        return nearest;
    }
}
