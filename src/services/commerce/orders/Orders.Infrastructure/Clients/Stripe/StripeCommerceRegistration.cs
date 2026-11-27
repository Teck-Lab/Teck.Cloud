using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Clients.Stripe;

/// <summary>Registers the Stripe Billing adapter under the Orders service composition.</summary>
public static class StripeCommerceRegistration
{
    /// <summary>Adds the Stripe Billing adapter; invalid or absent billing settings leave provider operations unavailable.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration source for CommerceOrders:Stripe.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddCommerceStripe(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = StripeCommerceOptions.Read(configuration);
        services.TryAddSingleton(TimeProvider.System);
        _ = services.AddSingleton<ICommerceBilling>(provider => new StripeCommerceBilling(
            options,
            provider.GetRequiredService<TimeProvider>()));
        return services;
    }
}
