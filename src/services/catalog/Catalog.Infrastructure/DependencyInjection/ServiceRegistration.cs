// <copyright file="ServiceRegistration.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ServiceScan.SourceGenerator;

namespace Catalog.Infrastructure.DependencyInjection;

/// <summary>
/// Provides generated service registration methods for Catalog services.
/// </summary>
public static partial class ServiceRegistration
{
    /// <summary>
    /// Registers application services from Catalog.Application as implemented interfaces (scoped).
    /// </summary>
    [GenerateServiceRegistrations(
        FromAssemblyOf = typeof(Catalog.Application.ICatalogApplication),
        TypeNameFilter = "*",
        AsImplementedInterfaces = true,
        Lifetime = ServiceLifetime.Scoped,
        ExcludeByTypeName = "*DbContext",
        ExcludeAssignableTo = typeof(MemoryPack.IMemoryPackable<>))]
    public static partial IServiceCollection AddCatalogApplicationServices(this IServiceCollection services);

    /// <summary>
    /// Registers repository classes from Catalog.Infrastructure as implemented interfaces (scoped).
    /// </summary>
    [GenerateServiceRegistrations(
        FromAssemblyOf = typeof(Catalog.Infrastructure.Persistence.Repositories.Read.BrandReadRepository),
        TypeNameFilter = "*",
        AsImplementedInterfaces = true,
        Lifetime = ServiceLifetime.Scoped,
        ExcludeByTypeName = "*DbContext")]
    public static partial IServiceCollection AddCatalogInfrastructureRepositories(this IServiceCollection services);
}
