// <copyright file="DatabaseServiceExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Billing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using SharedKernel.Core.Pricing;

namespace Billing.Infrastructure.DependencyInjection;

/// <summary>
/// Provides extensions for configuring database-related services for the Billing API.
/// </summary>
public static class DatabaseServiceExtensions
{
    /// <summary>
    /// Adds the Billing write database context.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="migrationsAssembly">The assembly containing migrations.</param>
    /// <param name="defaultWriteConnectionString">The default write connection string.</param>
    /// <param name="provider">The deployment-selected database provider.</param>
    public static void AddWriteDatabase(
        this WebApplicationBuilder builder,
        Assembly? migrationsAssembly,
        string defaultWriteConnectionString,
        DatabaseProvider provider)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddDbContextFactory<BillingDbContext>(options =>
        {
            SharedKernel.Persistence.Database.Extensions.ConfigureProviderDbContextOptions(
                options,
                defaultWriteConnectionString,
                migrationsAssembly,
                provider);
        });
    }
}
