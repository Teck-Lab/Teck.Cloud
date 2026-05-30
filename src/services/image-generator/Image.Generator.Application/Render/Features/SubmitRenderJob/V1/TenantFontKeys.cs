// <copyright file="TenantFontKeys.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1;

internal static class TenantFontKeys
{
    internal const string Prefix = "tenant-font:";

    internal static bool TryParseTenantFontKey(string? fontFamily, out string fontKey)
    {
        fontKey = string.Empty;

        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            return false;
        }

        if (!fontFamily.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string candidate = fontFamily[Prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        fontKey = candidate;
        return true;
    }
}
