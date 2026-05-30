// <copyright file="SolumBmpEncoder.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;
using SkiaSharp;

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1.ProviderEncoders;

/// <summary>
/// Encodes images for Solum ESL displays.
/// Outputs 1bpp or 2bpp BMP with exact color mapping.
/// </summary>
internal sealed class SolumBmpEncoder : IProviderEncoder
{
    public string ProviderName => "solum";

    public byte[] Encode(SKImage image, ProviderProfile profile)
    {
        int width = image.Width;
        int height = image.Height;
        bool hasRed = profile.SupportedColors.Contains("red", StringComparer.OrdinalIgnoreCase);
        int bitsPerPixel = hasRed ? 2 : 1;

        // Row size must be aligned to 4 bytes
        int rowSize = (((width * bitsPerPixel) + 31) / 32) * 4;
        int pixelDataSize = rowSize * height;

        // 2 colors * 4 bytes or 4 colors * 4 bytes
        int paletteSize = bitsPerPixel == 1 ? 8 : 16;
        int headerSize = 14 + 40 + paletteSize;
        int fileSize = headerSize + pixelDataSize;

        byte[] output = new byte[fileSize];

        // BMP file header (14 bytes)
        output[0] = (byte)'B';
        output[1] = (byte)'M';
        BitConverter.GetBytes(fileSize).CopyTo(output, 2);
        BitConverter.GetBytes(0).CopyTo(output, 6); // Reserved
        BitConverter.GetBytes(headerSize).CopyTo(output, 10);

        // DIB header (BITMAPINFOHEADER, 40 bytes)
        BitConverter.GetBytes(40).CopyTo(output, 14); // Header size
        BitConverter.GetBytes(width).CopyTo(output, 18);
        BitConverter.GetBytes(height).CopyTo(output, 22);
        BitConverter.GetBytes((ushort)1).CopyTo(output, 26); // Planes
        BitConverter.GetBytes((ushort)bitsPerPixel).CopyTo(output, 28);
        BitConverter.GetBytes(0).CopyTo(output, 30); // Compression (BI_RGB)
        BitConverter.GetBytes(pixelDataSize).CopyTo(output, 34);
        BitConverter.GetBytes(2835).CopyTo(output, 38); // X pixels per meter
        BitConverter.GetBytes(2835).CopyTo(output, 42); // Y pixels per meter
        BitConverter.GetBytes(bitsPerPixel == 1 ? 2u : 4u).CopyTo(output, 46); // Colors in palette
        BitConverter.GetBytes(0u).CopyTo(output, 50); // Important colors

        // Color palette (BGRA)
        int paletteOffset = 54;
        if (bitsPerPixel == 1)
        {
            // Black
            output[paletteOffset] = 0;
            output[paletteOffset + 1] = 0;
            output[paletteOffset + 2] = 0;
            output[paletteOffset + 3] = 0;

            // White
            output[paletteOffset + 4] = 255;
            output[paletteOffset + 5] = 255;
            output[paletteOffset + 6] = 255;
            output[paletteOffset + 7] = 0;
        }
        else
        {
            // Black, White, Red, Yellow (BGRA order)
            byte[][] colors =
            [
                [0, 0, 0, 0],
                [255, 255, 255, 0],
                [0, 0, 255, 0],
                [0, 255, 255, 0],
            ];

            for (int colorIndex = 0; colorIndex < 4; colorIndex++)
            {
                output[paletteOffset + (colorIndex * 4)] = colors[colorIndex][0];
                output[paletteOffset + (colorIndex * 4) + 1] = colors[colorIndex][1];
                output[paletteOffset + (colorIndex * 4) + 2] = colors[colorIndex][2];
                output[paletteOffset + (colorIndex * 4) + 3] = colors[colorIndex][3];
            }
        }

        // Pixel data
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

        int pixelOffset = headerSize;

        // BMP is bottom-up
        for (int row = height - 1; row >= 0; row--)
        {
            for (int column = 0; column < width; column++)
            {
                int pixelIndex = ((row * width) + column) * 4;
                byte red = pixels[pixelIndex];
                byte green = pixels[pixelIndex + 1];
                byte blue = pixels[pixelIndex + 2];

                int colorIndex = ClassifyPixel(red, green, blue, hasRed);
                int bitOffset = column * bitsPerPixel;
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

                    output[pixelOffset + byteOffset] |= (byte)(colorIndex << shift);
                }
                else
                {
                    if (colorIndex == 0)
                    {
                        output[pixelOffset + byteOffset] |= (byte)(1 << shift);
                    }
                }
            }

            pixelOffset += rowSize;
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
                return 2;
            }
        }

        return distBlack < distWhite ? 0 : 1;
    }
}
