// <copyright file="HanshowBinaryEncoder.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;
using SkiaSharp;

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1.ProviderEncoders;

/// <summary>
/// Encodes images for Hanshow ESL displays.
/// Outputs a simple binary bitmap: 1 bit per pixel for B/W, 2 bits per pixel for B/W/R.
/// </summary>
internal sealed class HanshowBinaryEncoder : IProviderEncoder
{
    public string ProviderName => "hanshow";

    public byte[] Encode(SKImage image, ProviderProfile profile)
    {
        int width = image.Width;
        int height = image.Height;

        // Read pixels as BGRA
        SKImageInfo info = new(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
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

        bool hasRed = profile.SupportedColors.Contains("red", StringComparer.OrdinalIgnoreCase);
        int bitsPerPixel = hasRed ? 2 : 1;
        int bytesPerRow = ((width * bitsPerPixel) + 7) / 8;
        byte[] output = new byte[bytesPerRow * height];

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int pixelIndex = ((row * width) + column) * 4;
                byte blue = pixels[pixelIndex];
                byte green = pixels[pixelIndex + 1];
                byte red = pixels[pixelIndex + 2];

                int bitValue = ClassifyPixel(red, green, blue, hasRed);
                int bitOffset = (row * bytesPerRow * 8) + (column * bitsPerPixel);
                int byteOffset = bitOffset / 8;
                int shift = 7 - (bitOffset % 8);

                if (bitsPerPixel == 2)
                {
                    shift -= bitOffset % 8;
                    if (shift < 0)
                    {
                        byteOffset++;
                        shift += 8;
                    }

                    output[byteOffset] |= (byte)(bitValue << shift);
                }
                else
                {
                    if (bitValue == 1)
                    {
                        output[byteOffset] |= (byte)(1 << shift);
                    }
                }
            }
        }

        return output;
    }

    private static int ClassifyPixel(byte red, byte green, byte blue, bool hasRed)
    {
        int distBlack = (red * red) + (green * green) + (blue * blue);
        int distWhite = ((255 - red) * (255 - red)) + ((255 - green) * (255 - green)) + ((255 - blue) * (255 - blue));

        if (hasRed)
        {
            int distRed = ((255 - red) * (255 - red)) + (green * green) + (blue * blue);

            if (distRed < distBlack && distRed < distWhite)
            {
                return 2; // Red
            }
        }

        return distBlack < distWhite ? 1 : 0; // Black : White
    }
}
