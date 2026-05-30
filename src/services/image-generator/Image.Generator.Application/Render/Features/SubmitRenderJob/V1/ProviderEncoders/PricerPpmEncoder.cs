// <copyright file="PricerPpmEncoder.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Globalization;
using SkiaSharp;

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1.ProviderEncoders;

/// <summary>
/// Encodes images for Pricer ESL displays.
/// Outputs ASCII PPM (Portable Pixmap) with exact color mapping.
/// </summary>
internal sealed class PricerPpmEncoder : IProviderEncoder
{
    public string ProviderName => "pricer";

    public byte[] Encode(SKImage image, ProviderProfile profile)
    {
        int width = image.Width;
        int height = image.Height;

        SKImageInfo info = new(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        byte[] pixels = new byte[width * height * 4];
        System.Runtime.InteropServices.GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            image.ReadPixels(info, handle.AddrOfPinnedObject(), width * 4, 0, 0);
        }
        finally
        {
            handle.Free();
        }

        bool hasRed = profile.SupportedColors.Contains("red", StringComparer.OrdinalIgnoreCase);
        bool hasYellow = profile.SupportedColors.Contains("yellow", StringComparer.OrdinalIgnoreCase);

        StringBuilder ppm = new();
        ppm.AppendLine("P3");
        ppm.AppendLine($"{width} {height}");
        ppm.AppendLine("255");

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int pixelIndex = ((row * width) + column) * 4;
                byte red = pixels[pixelIndex];
                byte green = pixels[pixelIndex + 1];
                byte blue = pixels[pixelIndex + 2];

                (byte paletteRed, byte paletteGreen, byte paletteBlue) = QuantizeToPalette(red, green, blue, hasRed, hasYellow);
                ppm.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} ", paletteRed, paletteGreen, paletteBlue);
            }

            ppm.AppendLine();
        }

        return System.Text.Encoding.ASCII.GetBytes(ppm.ToString());
    }

    private static (byte Red, byte Green, byte Blue) QuantizeToPalette(byte red, byte green, byte blue, bool hasRed, bool hasYellow)
    {
        int distBlack = (red * red) + (green * green) + (blue * blue);
        int distWhite = ((255 - red) * (255 - red)) + ((255 - green) * (255 - green)) + ((255 - blue) * (255 - blue));
        int bestDistance = Math.Min(distBlack, distWhite);
        byte bestR = distBlack < distWhite ? (byte)0 : (byte)255;
        byte bestG = bestR;
        byte bestB = bestR;

        if (hasRed)
        {
            int distRed = ((255 - red) * (255 - red)) + (green * green) + (blue * blue);
            if (distRed < bestDistance)
            {
                bestDistance = distRed;
                bestR = 255;
                bestG = 0;
                bestB = 0;
            }
        }

        if (hasYellow)
        {
            int distYellow = ((255 - red) * (255 - red)) + ((255 - green) * (255 - green)) + (blue * blue);
            if (distYellow < bestDistance)
            {
                bestR = 255;
                bestG = 255;
                bestB = 0;
            }
        }

        return (bestR, bestG, bestB);
    }
}
