// <copyright file="InfrastructureServiceExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Basket.Application.Basket.Repositories;
using Basket.Application.Common.Interfaces;
using Basket.Infrastructure.Catalog;
using Basket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Basket.Infrastructure.DependencyInjection;

/// <summary>
/// Registers Basket infrastructure services.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Adds infrastructure dependencies for basket service.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string? writeConnectionString = configuration.GetConnectionString("db-write");

        services.AddDbContextFactory<BasketPersistenceDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(writeConnectionString))
            {
                options.UseNpgsql(writeConnectionString);
                return;
            }

            string fallbackSqlitePath = Path.Combine(AppContext.BaseDirectory, "basket-signed-in.db");
            options.UseSqlite($"Data Source={fallbackSqlitePath}");
        });

        services.AddHostedService<BasketPersistenceInitializationService>();
        services.AddSingleton<IBasketRepository, HybridBasketRepository>();
        services.AddSingleton<ICatalogValidationClient, CatalogValidationClient>();
    }

    private sealed class BasketPersistenceInitializationService(IDbContextFactory<BasketPersistenceDbContext> dbContextFactory)
        : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using BasketPersistenceDbContext dbContext = await dbContextFactory
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
