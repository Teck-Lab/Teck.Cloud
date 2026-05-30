// <copyright file="DatabaseServiceExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Basket.Application.Basket.Repositories;
using Basket.Application.Common.Interfaces;
using Basket.Infrastructure.Catalog;
using Basket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Pricing;

namespace Basket.Infrastructure.DependencyInjection;

/// <summary>
/// Provides extensions for configuring database-related services for the Basket API.
/// </summary>
public static class DatabaseServiceExtensions
{
    /// <summary>
    /// Adds database contexts and repository registrations with CQRS shape parity.
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

        builder.Services.AddDbContextFactory<BasketPersistenceDbContext>(options =>
        {
            SharedKernel.Persistence.Database.Extensions.ConfigureProviderDbContextOptions(
                options,
                defaultWriteConnectionString,
                migrationsAssembly,
                provider);
        });

        builder.Services.AddDbContextFactory<BasketReadDbContext>(options =>
        {
            SharedKernel.Persistence.Database.Extensions.ConfigureProviderDbContextOptions(
                options,
                defaultReadConnectionString,
                migrationsAssembly: null,
                provider);
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        builder.Services.AddSingleton<IBasketRepository, HybridBasketRepository>();
        builder.Services.AddSingleton<ICatalogValidationClient, CatalogValidationClient>();
    }
}
