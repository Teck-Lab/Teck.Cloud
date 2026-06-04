// <copyright file="ServiceRegistration.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ServiceScan.SourceGenerator;

namespace Customer.Infrastructure.DependencyInjection;

/// <summary>
/// Provides generated service registration methods for Customer services.
/// </summary>
public static partial class ServiceRegistration
{
    /// <summary>
    /// Registers repository classes from Customer.Infrastructure as implemented interfaces (scoped).
    /// </summary>
    [GenerateServiceRegistrations(
        FromAssemblyOf = typeof(Customer.Infrastructure.Persistence.Repositories.Read.TenantReadRepository),
        TypeNameFilter = "*",
        AsImplementedInterfaces = true,
        Lifetime = ServiceLifetime.Scoped,
        ExcludeByTypeName = "*DbContext")]
    public static partial IServiceCollection AddCustomerInfrastructureRepositories(this IServiceCollection services);
}
