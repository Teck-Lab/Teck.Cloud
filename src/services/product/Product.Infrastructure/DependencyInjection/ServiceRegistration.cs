// <copyright file="ServiceRegistration.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ServiceScan.SourceGenerator;

namespace Product.Infrastructure.DependencyInjection;

/// <summary>
/// Provides generated service registration methods for Product infrastructure.
/// </summary>
public static partial class ServiceRegistration
{
    /// <summary>
    /// Registers repository classes from Product.Infrastructure as implemented interfaces (scoped).
    /// </summary>
    [GenerateServiceRegistrations(
        FromAssemblyOf = typeof(Product.Infrastructure.Persistence.Repositories.Read.DbProductReadRepository),
        TypeNameFilter = "*",
        AsImplementedInterfaces = true,
        Lifetime = ServiceLifetime.Scoped,
        ExcludeByTypeName = "*DbContext")]
    public static partial IServiceCollection AddProductInfrastructureRepositories(this IServiceCollection services);
}
