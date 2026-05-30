// <copyright file="RenderSurfacePool.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using SkiaSharp;

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1;

/// <summary>
/// Pools SKSurface instances for common label sizes to reduce unmanaged memory allocation.
/// </summary>
internal sealed class RenderSurfacePool : IDisposable
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<SKSurface>> _pool = new();
    private readonly ConcurrentDictionary<string, SKImageInfo> _infoCache = new();
    private bool _disposed;

    /// <summary>
    /// Gets or creates a surface for the specified dimensions.
    /// </summary>
    public SKSurface Rent(int width, int height)
    {
        string key = $"{width}x{height}";

        if (_pool.TryGetValue(key, out ConcurrentBag<SKSurface>? bag) && bag.TryTake(out SKSurface? surface))
        {
            // Clear the canvas for reuse
            surface.Canvas.Clear(SKColors.Transparent);
            return surface;
        }

        // Create new surface
        SKImageInfo info = _infoCache.GetOrAdd(key, _ => new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        return SKSurface.Create(info);
    }

    /// <summary>
    /// Returns a surface to the pool for reuse.
    /// </summary>
    public void Return(SKSurface surface)
    {
        if (_disposed || surface == null)
        {
            surface?.Dispose();
            return;
        }

        string key = $"{surface.Canvas.DeviceClipBounds.Width}x{surface.Canvas.DeviceClipBounds.Height}";
        ConcurrentBag<SKSurface> bag = _pool.GetOrAdd(key, _ => new ConcurrentBag<SKSurface>());
        bag.Add(surface);
    }

    /// <summary>
    /// Clears the pool and disposes all surfaces.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (ConcurrentBag<SKSurface> bag in _pool.Values)
        {
            while (bag.TryTake(out SKSurface? surface))
            {
                surface?.Dispose();
            }
        }

        _pool.Clear();
    }
}
