// <copyright file="ServiceRegistration.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ServiceScan.SourceGenerator;

namespace Billing.Infrastructure.DependencyInjection;

/// <summary>
/// Provides generated service registration methods for Billing infrastructure services.
/// </summary>
public static partial class ServiceRegistration
{
    /// <summary>
    /// Registers repository classes from Billing.Infrastructure as implemented interfaces (scoped).
    /// </summary>
    [GenerateServiceRegistrations(
        FromAssemblyOf = typeof(Billing.Infrastructure.Persistence.Repositories.Read.BillingTransactionReadRepository),
        TypeNameFilter = "*",
        AsImplementedInterfaces = true,
        Lifetime = ServiceLifetime.Scoped,
        ExcludeByTypeName = "*DbContext")]
    public static partial IServiceCollection AddBillingInfrastructureRepositories(this IServiceCollection services);
}
