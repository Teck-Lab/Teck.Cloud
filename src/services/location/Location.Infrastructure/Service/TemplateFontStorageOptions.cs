// <copyright file="TemplateFontStorageOptions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Infrastructure.Service;

public sealed class TemplateFontStorageOptions
{
    public const string Section = "TemplateFontStorage";

    public string ConnectionString { get; init; } = string.Empty;

    public string LocalDirectory { get; init; } = Path.Combine(Path.GetTempPath(), "teck-cloud", "location", "template-fonts");

    public string ObjectKeyTemplate { get; init; } = "tenant-fonts/{tenantId}/{fontKey}";

    public int MaxFontBytes { get; init; } = 5242880;
}
