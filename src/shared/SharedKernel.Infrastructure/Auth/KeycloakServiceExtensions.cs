using Keycloak.AuthServices.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace SharedKernel.Infrastructure.Auth;

/// <summary>Composes upstream Keycloak authentication and authorization with explicit transport boundaries.</summary>
public static class KeycloakServiceExtensions
{
    /// <summary>Adds JWT authentication, native organization requirements and Authorization Services.</summary>
    /// <param name="services">Host registrations.</param>
    /// <param name="settings">Trusted identity endpoints and the service audience.</param>
    /// <returns>The Authorization Services HTTP builder for service-specific configuration.</returns>
    public static IHttpClientBuilder AddFoundationKeycloakAuthentication(this IServiceCollection services, KeycloakServiceOptions settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var issuer = Validate(settings);
        var server = settings.ServerUrl.AbsoluteUri.TrimEnd('/') + "/";
        var realmUrl = server + "realms/" + Uri.EscapeDataString(settings.Realm);
        _ = services.AddKeycloakWebApiAuthentication(
            options =>
        {
            options.AuthServerUrl = server;
            options.Realm = settings.Realm;
            options.Resource = settings.Audience;
            options.VerifyTokenAudience = true;
            options.TokenClockSkew = TimeSpan.FromSeconds(30);
        },
            options =>
        {
            options.Authority = issuer;
            options.MetadataAddress = realmUrl + "/.well-known/openid-configuration";
            options.RequireHttpsMetadata = settings.Transport == IdentityTransport.Https;
            options.MapInboundClaims = false;
            options.SaveToken = true;
            options.IncludeErrorDetails = false;
            options.BackchannelTimeout = TimeSpan.FromSeconds(10);
            options.BackchannelHttpHandler = CreateHandler();
            options.TokenValidationParameters.ValidIssuer = issuer;
            options.TokenValidationParameters.IssuerValidator = (candidate, _, _) =>
                string.Equals(candidate, issuer, StringComparison.Ordinal)
                    ? candidate
                    : throw new SecurityTokenInvalidIssuerException("The token issuer does not match the configured public issuer.");
            options.TokenValidationParameters.ValidAudience = settings.Audience;
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidateAudience = true;
            options.TokenValidationParameters.ValidateLifetime = true;
            options.TokenValidationParameters.RequireSignedTokens = true;
            options.TokenValidationParameters.RequireExpirationTime = true;
        });
        _ = services.AddKeycloakAuthorization(options =>
        {
            options.AuthServerUrl = server;
            options.Realm = settings.Realm;
            options.Resource = settings.Audience;
        });
        return services.AddAuthorizationServer(
            options =>
        {
            options.AuthServerUrl = server;
            options.Realm = settings.Realm;
            options.Resource = settings.Audience;
            options.SourceAuthenticationScheme = JwtBearerDefaults.AuthenticationScheme;
        },
            client => client.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(CreateHandler);
    }

    private static SocketsHttpHandler CreateHandler() => new() { AllowAutoRedirect = false, UseCookies = false };

    private static string Validate(KeycloakServiceOptions settings)
    {
        IdentityTransportPolicy.Validate(settings.ServerUrl, settings.Transport);
        if (string.IsNullOrWhiteSpace(settings.Realm) || string.IsNullOrWhiteSpace(settings.Audience)
            || settings.ServerUrl.Query.Length != 0 || settings.Realm.IndexOfAny(['/', '?', '#']) >= 0)
        {
            throw new InvalidOperationException("A fixed Keycloak server, realm and audience are required.");
        }

        if (settings.Transport == IdentityTransport.AmbientMesh && settings.PublicIssuer is null)
        {
            throw new InvalidOperationException("Ambient HTTP requires the canonical public HTTPS issuer.");
        }

        var issuer = settings.PublicIssuer ?? new Uri(settings.ServerUrl.AbsoluteUri.TrimEnd('/') + "/realms/" + Uri.EscapeDataString(settings.Realm));
        IdentityTransportPolicy.Validate(issuer, settings.Transport == IdentityTransport.IsolatedDevelopment ? IdentityTransport.IsolatedDevelopment : IdentityTransport.Https);
        return issuer.Query.Length == 0
            ? issuer.AbsoluteUri.TrimEnd('/')
            : throw new InvalidOperationException("The public issuer cannot contain a query.");
    }
}
