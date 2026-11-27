using Microsoft.Extensions.DependencyInjection;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Clients;
using Teck.Cloud.Commerce.Gateway.Services;
using LicensingV1 = Teck.Cloud.Commerce.Licensing.V1;
using OrdersV1 = Teck.Cloud.Commerce.Orders.V1;

namespace Teck.Cloud.Commerce.Gateway.Composition;

/// <summary>Registers only the internal commerce clients and edge orchestration; no persistence or messaging.</summary>
public static class CommerceGatewayServiceCollectionExtensions
{
    private const int MaximumResponseBytes = 16 * 1024 * 1024;

    /// <summary>Registers the customer edge over Orders and Licensing.</summary>
    /// <param name="services">Host registrations.</param>
    /// <param name="options">Validated internal endpoints and transport.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="InvalidOperationException">The customer edge has no Licensing address.</exception>
    public static IServiceCollection AddCustomerCommerceGateway(this IServiceCollection services, CommerceGatewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        _ = services.AddCommerceOrdersClient(options);
        _ = services.AddSingleton(options);
        _ = services.AddScoped<ICommerceOrdersGateway, GrpcCommerceOrdersGateway>();
        if (options.LicensingAddress is null)
        {
            throw new InvalidOperationException("The customer gateway requires Commerce:Licensing:Address.");
        }

        _ = services.AddGrpcClient<LicensingV1.Licensing.LicensingClient>(client => client.Address = options.LicensingAddress)
            .ConfigureChannel(channel => channel.MaxReceiveMessageSize = MaximumResponseBytes)
            .AddCommerceOperationCredentials(CommerceAuth.LicensingAudience, options.Transport);
        _ = services.AddScoped<ICommerceLicensingGateway, GrpcCommerceLicensingGateway>();
        _ = services.AddScoped<CustomerCommerceService>();
        return services;
    }

    /// <summary>Registers the staff edge over Orders only.</summary>
    /// <param name="services">Host registrations.</param>
    /// <param name="options">Validated internal endpoints and transport.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAdminCommerceGateway(this IServiceCollection services, CommerceGatewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        _ = services.AddCommerceOrdersClient(options);
        _ = services.AddSingleton(options);
        _ = services.AddScoped<ICommerceOrdersGateway, GrpcCommerceOrdersGateway>();
        _ = services.AddScoped<AdminCommerceService>();
        return services;
    }

    private static IServiceCollection AddCommerceOrdersClient(this IServiceCollection services, CommerceGatewayOptions options)
    {
        _ = services.AddGrpcClient<OrdersV1.Orders.OrdersClient>(client => client.Address = options.OrdersAddress)
            .ConfigureChannel(channel => channel.MaxReceiveMessageSize = MaximumResponseBytes)
            .AddCommerceOperationCredentials(CommerceAuth.OrdersAudience, options.Transport);
        return services;
    }
}
