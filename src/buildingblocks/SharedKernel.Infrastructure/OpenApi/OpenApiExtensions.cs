using System.Runtime.InteropServices;
using FastEndpoints.Swagger;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using NSwag;
using Scalar.AspNetCore;
using SharedKernel.Infrastructure.Options;

namespace SharedKernel.Infrastructure.OpenApi
{
    /// <summary>
    /// The open api extensions.
    /// </summary>
    public static class OpenApiExtensions
    {
        /// <summary>
        /// Add open api infrastructure.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="appOptions">The app options.</param>
        public static void AddOpenApiInfrastructure(
            this WebApplicationBuilder builder,
            AppOptions appOptions)
        {
            var keycloakOptions = builder.Configuration.GetKeycloakOptions<KeycloakAuthenticationOptions>();

            List<Action<DocumentOptions>> documentOptions = new List<Action<DocumentOptions>>();

            foreach (var apiVersion in appOptions.Versions)
            {
                Action<DocumentOptions> document = new(options =>
                {
                    options.EnableJWTBearerAuth = false;
                    options.MaxEndpointVersion = apiVersion;
                    options.DocumentSettings = setting =>
                    {
                        setting.Version = $"v{apiVersion}";
                        setting.Title = $"{appOptions.Name} API";
                        setting.DocumentName = $"v{apiVersion}";
                        setting.Description = appOptions.Description;

                        var (tokenEndpoint, authorizationEndpoint) = ResolveOAuthEndpoints(builder.Configuration, keycloakOptions);

                        setting.AddAuth(
                            "oAuth2",
                            AddOAuthScheme(tokenEndpoint, authorizationEndpoint),
                            ["openid", "profile", "email"]);
                    };
                });

                documentOptions.Add(document);
            }

            foreach (ref Action<DocumentOptions> option in CollectionsMarshal.AsSpan(documentOptions))
            {
                builder.Services.SwaggerDocument(option);
            }
        }

        /// <summary>
        /// Use open api infrastructure.
        /// </summary>
        /// <param name="app">The app.</param>
        /// <param name="appOptions"></param>
        public static void UseOpenApiInfrastructure(this WebApplication app, AppOptions appOptions)
        {
            app.UseSwaggerGen(document =>
            {
                document.Path = "/openapi/{documentName}/openapi.json";
            });
            app.MapScalarApiReference("docs", options =>
            {
                options.OpenApiRoutePattern = "/openapi/{documentName}/openapi.json";
                foreach (var apiVersion in appOptions.Versions)
                {
                    options.AddDocument($"v{apiVersion}");
                }

                options
                    .AddPreferredSecuritySchemes("oAuth2")
                    .AddAuthorizationCodeFlow("oAuth2", flow =>
                {
                    flow.ClientId = "scalar-ui";
                    flow.Pkce = Pkce.Sha256; // Enable PKCE
                });
            });
        }

        private static OpenApiSecurityScheme AddOAuthScheme(string tokenEndpoint, string authorizationEndpoint)
        {
            return new OpenApiSecurityScheme
            {
                Type = OpenApiSecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = authorizationEndpoint,
                        TokenUrl = tokenEndpoint,
                        Scopes = new Dictionary<string, string>
                        {
                            ["openid"] = "OpenID Connect",
                            ["profile"] = "User profile",
                            ["email"] = "User email"
                        }
                    }
                }
            };
        }

        private static (string TokenEndpoint, string AuthorizationEndpoint) ResolveOAuthEndpoints(
            IConfiguration configuration,
            KeycloakAuthenticationOptions? keycloakOptions)
        {
            const string fallbackRealmBase = "http://localhost:8080/realms/Teck.Cloud";

            var authority = configuration["Keycloak:Authority"]?.TrimEnd('/');
            var realmBase = keycloakOptions?.KeycloakUrlRealm?.TrimEnd('/');

            if (string.IsNullOrWhiteSpace(realmBase))
            {
                realmBase = authority;
            }

            if (string.IsNullOrWhiteSpace(realmBase))
            {
                realmBase = fallbackRealmBase;
            }

            var tokenEndpoint = keycloakOptions?.KeycloakTokenEndpoint;
            if (string.IsNullOrWhiteSpace(tokenEndpoint))
            {
                tokenEndpoint = $"{realmBase}/protocol/openid-connect/token";
            }

            var authorizationEndpoint = $"{realmBase}/protocol/openid-connect/auth";

            return (tokenEndpoint, authorizationEndpoint);
        }
    }
}
