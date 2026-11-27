using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.Ports;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGenerations;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;
using Teck.Cloud.Commerce.Licensing.Infrastructure.Clients;
using Teck.Cloud.Commerce.Licensing.Infrastructure.Database;
using Teck.Cloud.Commerce.Licensing.Infrastructure.Security;

namespace Teck.Cloud.Commerce.Licensing.Infrastructure;

/// <summary>Service-owned adapter registrations consumed by the licensing host.</summary>
public static class DependencyInjection
{
    /// <summary>Registers the validated key ring, document service, Orders port and repositories.</summary>
    /// <param name="services">Host registrations.</param>
    /// <param name="keyRing">The validated managed-secret key ring.</param>
    /// <returns>The configured registrations.</returns>
    public static IServiceCollection AddLicensingInfrastructure(this IServiceCollection services, LicensingKeyRingOptions keyRing)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(keyRing);
        _ = services.AddSingleton(keyRing);
        _ = services.AddSingleton<ILicenseDocumentService, StandardLicenseDocumentService>();
        _ = services.AddScoped<IOrdersEntitlementClient, OrdersEntitlementClient>();
        _ = services.AddRepositories<LicenseGrant, Guid, LicensingReadDbContext, LicensingWriteDbContext>();
        _ = services.AddRepositories<LicenseGeneration, Guid, LicensingReadDbContext, LicensingWriteDbContext>();
        return services;
    }
}
