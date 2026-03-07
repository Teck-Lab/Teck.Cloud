// <copyright file="CatalogVersionResolver.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305
using System.Reflection;

namespace Catalog.Api.Endpoints.V1.Service;

internal static class CatalogVersionResolver
{
    internal static string ResolveVersion()
    {
        Assembly assembly = typeof(CatalogVersionResolver).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}
