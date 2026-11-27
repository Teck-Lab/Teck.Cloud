using Microsoft.Extensions.DependencyInjection;
using Teck.Cloud.Commerce.Orders.Application.Orders.Catalog;

namespace Teck.Cloud.Commerce.Orders.Application;

/// <summary>Explicit application-service registrations; handlers are discovered by Wolverine.</summary>
public static class DependencyInjection
{
    /// <summary>Registers catalog policy and application helpers.</summary>
    /// <param name="services">Host registrations.</param>
    /// <returns>The configured registrations.</returns>
    public static IServiceCollection AddOrdersApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddSingleton<CatalogService>();
        return services;
    }
}
