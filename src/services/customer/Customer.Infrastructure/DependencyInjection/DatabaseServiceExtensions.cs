// <copyright file="DatabaseServiceExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Customer.Application.Common.Interfaces;
using Customer.Application.Tenants.Repositories;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using Customer.Infrastructure.Identity;
using Customer.Infrastructure.Persistence;
using Customer.Infrastructure.Persistence.Repositories.Read;
using Customer.Infrastructure.Persistence.Repositories.Write;
using Microsoft.AspNetCore.Builder;
using SharedKernel.Core.Database;
using SharedKernel.Core.Pricing;
using SharedKernel.Persistence.Database;

namespace Customer.Infrastructure.DependencyInjection;

/// <summary>
/// Provides extensions for configuring database-related services for the Customer API.
/// </summary>
public static class DatabaseServiceExtensions
{
    /// <summary>
    /// Adds database contexts and repositories with CQRS pattern support.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="migrationsAssembly">The assembly containing migrations.</param>
    /// <param name="defaultWriteConnectionString">The default write connection string.</param>
    /// <param name="defaultReadConnectionString">The default read connection string.</param>
    /// <param name="provider">The deployment-selected database provider.</param>
    public static void AddCqrsDatabase(
        this WebApplicationBuilder builder,
        Assembly? migrationsAssembly,
        string defaultWriteConnectionString,
        string defaultReadConnectionString,
        DatabaseProvider provider)
    {
        ArgumentNullException.ThrowIfNull(builder);

        Assembly resolvedMigrationsAssembly = migrationsAssembly ?? typeof(CustomerWriteDbContext).Assembly;

        builder.AddCustomDbContexts<CustomerWriteDbContext, CustomerReadDbContext>(
            resolvedMigrationsAssembly,
            defaultWriteConnectionString,
            defaultReadConnectionString,
            provider);

        builder.Services.AddScoped<ITenantReadRepository, TenantReadRepository>();
        builder.Services.AddScoped<ITenantWriteRepository, TenantWriteRepository>();
        builder.Services.AddScoped<ILicenseWriteRepository, LicenseWriteRepository>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<ITenantIdentityProvisioningService, KeycloakTenantIdentityProvisioningService>();
    }
}
