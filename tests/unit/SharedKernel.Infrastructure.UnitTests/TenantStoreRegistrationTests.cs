using Finbuckle.MultiTenant.Abstractions;
using Keycloak.AuthServices.Sdk.Kiota;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.MultiTenancy;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace SharedKernel.Infrastructure.UnitTests;

/// <summary>Checks that choosing the Keycloak store binds every tenant consumer consistently.</summary>
public sealed class TenantStoreRegistrationTests
{
    /// <summary>Earlier directory registrations cannot silently split request and Finbuckle authority.</summary>
    [Fact]
    public void ExplicitKeycloakSelectionReplacesEarlierTenantAuthorities()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton(Substitute.For<IFusionCache>());
        _ = services.AddSingleton(Substitute.For<ITenantDirectory>());
        _ = services.AddSingleton(Substitute.For<IMultiTenantStore<KeycloakTenantInfo>>());
        _ = services.AddFoundationKeycloakAdmin(new KeycloakAdminClientOptions
        {
            AuthServerUrl = "https://identity.test/",
            Realm = "teck",
            Resource = "organization-reader",
            Credentials = new() { Secret = "fixture-only" },
        });
        _ = services.AddKeycloakTenantStore();
        _ = services.AddKeycloakTenantStore();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var expected = scope.ServiceProvider.GetRequiredService<KeycloakTenantStore>();
        scope.ServiceProvider.GetRequiredService<ITenantDirectory>().ShouldBeSameAs(expected);
        scope.ServiceProvider.GetRequiredService<IMultiTenantStore<KeycloakTenantInfo>>().ShouldBeSameAs(expected);
        scope.ServiceProvider.GetServices<ITenantDirectory>().Count().ShouldBe(1);
        scope.ServiceProvider.GetServices<IMultiTenantStore<KeycloakTenantInfo>>().Count().ShouldBe(1);
    }
}
