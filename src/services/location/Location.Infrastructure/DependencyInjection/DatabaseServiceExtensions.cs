// <copyright file="DatabaseServiceExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Location.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using SharedKernel.Core.Pricing;
using SharedKernel.Persistence.Database.MultiTenant;

namespace Location.Infrastructure.DependencyInjection;

/// <summary>
/// Provides extensions for configuring database-related services for the Location API.
/// </summary>
public static class DatabaseServiceExtensions
{
    /// <summary>
    /// Adds database contexts with CQRS shape parity, scoped per tenant via Finbuckle.
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

        builder.AddHybridMultiTenantDbContexts<TemplateFontMetadataDbContext, LocationReadDbContext>(
            migrationsAssembly,
            defaultWriteConnectionString,
            defaultReadConnectionString,
            provider,
            serviceName: "location");
    }
}
