// <copyright file="ServiceRegistration.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ServiceScan.SourceGenerator;

namespace Device.Infrastructure.DependencyInjection;

/// <summary>
/// Provides generated service registration methods for Device services.
/// </summary>
public static partial class ServiceRegistration
{
    /// <summary>
    /// Registers repository classes from Device.Infrastructure as implemented interfaces (scoped).
    /// </summary>
    [GenerateServiceRegistrations(
        FromAssemblyOf = typeof(Device.Infrastructure.Persistence.Repositories.Read.DbDeviceDefinitionReadRepository),
        TypeNameFilter = "*",
        AsImplementedInterfaces = true,
        Lifetime = ServiceLifetime.Scoped,
        ExcludeByTypeName = "*DbContext")]
    public static partial IServiceCollection AddDeviceInfrastructureRepositories(this IServiceCollection services);
}
