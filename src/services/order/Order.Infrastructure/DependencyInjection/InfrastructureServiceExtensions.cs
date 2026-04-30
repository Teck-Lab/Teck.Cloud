// <copyright file="InfrastructureServiceExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.Extensions.Configuration;
using Order.Application.Common.Interfaces;
using Order.Application.Orders.Repositories;
using Order.Infrastructure.Basket;
using Order.Infrastructure.Catalog;
using Order.Infrastructure.Persistence;

namespace Order.Infrastructure.DependencyInjection;

/// <summary>
/// Registers Order infrastructure services.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Adds infrastructure dependencies for order service.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

        services.AddHttpClient<IBasketSnapshotClient, BasketSnapshotClient>(
            (_, client) =>
            {
                string? basketBaseUrl = configuration["Services:BasketBaseUrl"];
                if (string.IsNullOrWhiteSpace(basketBaseUrl))
                {
                    return;
                }

                if (Uri.TryCreate(basketBaseUrl, UriKind.Absolute, out Uri? uri))
                {
                    client.BaseAddress = uri;
                }
            });

        services.AddSingleton<ICatalogValidationClient, CatalogValidationClient>();
    }
}
