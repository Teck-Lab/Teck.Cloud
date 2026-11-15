using Keycloak.AuthServices.Authorization.AuthorizationServer;
using Keycloak.AuthServices.Sdk.Admin;
using Keycloak.AuthServices.Sdk.Kiota.Admin;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel.Infrastructure.Auth;
using Shouldly;
using Xunit;
using AdminOptions = Keycloak.AuthServices.Sdk.Kiota.KeycloakAdminClientOptions;

namespace SharedKernel.Infrastructure.UnitTests;

/// <summary>Validates upstream identity registrations and explicit trust boundaries.</summary>
public sealed class KeycloakCompositionTests
{
    /// <summary>Mesh metadata can use HTTP without weakening the public issuer or signed-token validation.</summary>
    [Fact]
    public void AmbientBackchannelPreservesCanonicalIssuerAndAudience()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddFoundationKeycloakAuthentication(new KeycloakServiceOptions
        {
            ServerUrl = new Uri("http://keycloak.identity.svc:8080/"),
            PublicIssuer = new Uri("https://identity.example.test/realms/teck"),
            Realm = "teck",
            Audience = "customer-service",
            Transport = IdentityTransport.AmbientMesh,
        });
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        options.Authority.ShouldBe("https://identity.example.test/realms/teck");
        options.MetadataAddress.ShouldBe("http://keycloak.identity.svc:8080/realms/teck/.well-known/openid-configuration");
        options.TokenValidationParameters.ValidIssuer.ShouldBe(options.Authority);
        options.TokenValidationParameters.ValidAudience.ShouldBe("customer-service");
        options.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
        options.TokenValidationParameters.ValidateAudience.ShouldBeTrue();
        options.TokenValidationParameters.ValidateLifetime.ShouldBeTrue();
        options.TokenValidationParameters.RequireSignedTokens.ShouldBeTrue();
        options.TokenValidationParameters.RequireExpirationTime.ShouldBeTrue();
        options.MapInboundClaims.ShouldBeFalse();
        options.RequireHttpsMetadata.ShouldBeFalse();
        options.BackchannelTimeout.ShouldBe(TimeSpan.FromSeconds(10));
        var handler = options.BackchannelHttpHandler.ShouldBeOfType<SocketsHttpHandler>();
        handler.AllowAutoRedirect.ShouldBeFalse();
        handler.Dispose();
        _ = provider.GetRequiredService<IAuthorizationServerClient>();
    }

    /// <summary>Unprotected HTTP and unknown issuer boundaries fail during registration.</summary>
    /// <param name="transport">Configured transport mode.</param>
    /// <param name="issuer">Optional canonical issuer.</param>
    [Theory]
    [InlineData(IdentityTransport.Https, null)]
    [InlineData(IdentityTransport.AmbientMesh, null)]
    [InlineData(IdentityTransport.AmbientMesh, "http://identity.example.test/realms/teck")]
    public void HttpNeedsAnExplicitProtectedBoundary(IdentityTransport transport, string? issuer)
    {
        var services = new ServiceCollection();
        _ = Should.Throw<InvalidOperationException>(() => services.AddFoundationKeycloakAuthentication(new KeycloakServiceOptions
        {
            ServerUrl = new Uri("http://keycloak:8080/"),
            PublicIssuer = issuer is null ? null : new Uri(issuer),
            Realm = "teck",
            Audience = "customer-service",
            Transport = transport,
        }));
    }

    /// <summary>Default application TLS keeps issuer and metadata transport protected.</summary>
    [Fact]
    public void HttpsIsTheDefault()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddFoundationKeycloakAuthentication(new KeycloakServiceOptions
        {
            ServerUrl = new Uri("https://identity.example.test/auth/"),
            Realm = "teck",
            Audience = "customer-service",
        });
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        options.RequireHttpsMetadata.ShouldBeTrue();
        options.Authority.ShouldBe("https://identity.example.test/auth/realms/teck");
        options.BackchannelHttpHandler!.Dispose();
    }

    /// <summary>Admin composition preserves native options and resolves both official SDK clients.</summary>
    [Fact]
    public void AdminUsesNativeClientsAndRejectsUnsafeConfiguration()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        var options = new AdminOptions
        {
            AuthServerUrl = "http://identity.test:8080/",
            Realm = "teck",
            Resource = "customer-organization-reader",
            Credentials = new Keycloak.AuthServices.Common.KeycloakClientInstallationCredentials { Secret = "fixture-only-secret" },
        };
        _ = Should.Throw<InvalidOperationException>(() => services.AddFoundationKeycloakAdmin(options));
        _ = services.AddFoundationKeycloakAdmin(options, IdentityTransport.IsolatedDevelopment);
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<KeycloakAdminApiClient>();
        _ = provider.GetRequiredService<IKeycloakClient>();
        var registered = provider.GetRequiredService<IOptions<AdminOptions>>().Value;
        registered.Realm.ShouldBe("teck");
        registered.Resource.ShouldBe("customer-organization-reader");
    }
}
