// <copyright file="InfrastructureServiceExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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

        string? writeConnectionString = configuration.GetConnectionString("db-write");

        services.AddDbContextFactory<OrderPersistenceDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(writeConnectionString))
            {
                options.UseNpgsql(writeConnectionString);
                return;
            }

            string fallbackSqlitePath = Path.Combine(AppContext.BaseDirectory, "order-drafts.db");
            options.UseSqlite($"Data Source={fallbackSqlitePath}");
        });

        services.AddHostedService<OrderPersistenceInitializationService>();
        services.AddSingleton<IOrderRepository, EfOrderRepository>();

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

    private sealed class OrderPersistenceInitializationService(IDbContextFactory<OrderPersistenceDbContext> dbContextFactory)
        : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using OrderPersistenceDbContext dbContext = await dbContextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
