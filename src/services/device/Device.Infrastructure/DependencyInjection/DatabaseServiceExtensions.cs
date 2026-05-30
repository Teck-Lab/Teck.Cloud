// <copyright file="DatabaseServiceExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Device.Application.Displays.Abstractions;
using Device.Domain.AccessPoints;
using Device.Infrastructure.Persistence;
using Device.Infrastructure.Persistence.Repositories.Read;
using Device.Infrastructure.Persistence.Repositories.Write;
using Microsoft.AspNetCore.Builder;
using SharedKernel.Core.Database;
using SharedKernel.Core.Pricing;
using SharedKernel.Persistence.Database.EFCore;
using SharedKernel.Persistence.Database.MultiTenant;

namespace Device.Infrastructure.DependencyInjection;

/// <summary>
/// Provides extensions for configuring database-related services for the Device API.
/// </summary>
public static class DatabaseServiceExtensions
{
    /// <summary>
    /// Adds database contexts with CQRS shape parity.
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

        builder.AddHybridMultiTenantDbContexts<DeviceWriteDbContext, DeviceReadDbContext>(
            migrationsAssembly,
            defaultWriteConnectionString,
            defaultReadConnectionString,
            provider,
            serviceName: "device");

        builder.Services.AddScoped<IDisplayWriteRepository, DbDisplayWriteRepository>();
        builder.Services.AddScoped<IDisplayReadRepository, DbDisplayReadRepository>();
        builder.Services.AddScoped<IAccessPointWriteRepository, DbAccessPointWriteRepository>();
        builder.Services.AddScoped<IAccessPointReadRepository, DbAccessPointReadRepository>();

        builder.Services.AddScoped<IUnitOfWork>(sp =>
        {
            DeviceWriteDbContext writeDbContext = sp.GetRequiredService<DeviceWriteDbContext>();
            return new UnitOfWork<DeviceWriteDbContext>(writeDbContext);
        });
    }
}
