// <copyright file="ServiceRegistration.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ServiceScan.SourceGenerator;

namespace Location.Infrastructure.DependencyInjection;

/// <summary>
/// Provides generated service registration methods for Location services.
/// </summary>
public static partial class ServiceRegistration
{
    /// <summary>
    /// Registers repository classes from Location.Infrastructure as implemented interfaces (scoped).
    /// </summary>
    [GenerateServiceRegistrations(
        FromAssemblyOf = typeof(Location.Infrastructure.Service.DbDisplayModelReadRepository),
        TypeNameFilter = "*",
        AsImplementedInterfaces = true,
        Lifetime = ServiceLifetime.Scoped,
        ExcludeByTypeName = "*DbContext,*SeedItem")]
    public static partial IServiceCollection AddLocationInfrastructureRepositories(this IServiceCollection services);
}
