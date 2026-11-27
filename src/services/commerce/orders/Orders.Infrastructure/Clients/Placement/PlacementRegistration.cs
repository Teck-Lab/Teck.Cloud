using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Clients.Placement;

/// <summary>Registers the read-only Npgsql placement probe under the Orders service composition.</summary>
public static class PlacementRegistration
{
    /// <summary>Adds the allowlisted read-only placement probe from CommerceOrders:Placements.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration source for the registry and connection-string references.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">A structurally invalid or duplicate registry entry was configured.</exception>
    public static IServiceCollection AddCommercePlacement(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = PlacementOptions.Read(configuration);
        services.TryAddSingleton(TimeProvider.System);
        _ = services.AddSingleton<IPlacementProbe>(provider => new PlacementNpgsqlProbe(
            options,
            configuration,
            provider.GetRequiredService<TimeProvider>()));
        return services;
    }
}
