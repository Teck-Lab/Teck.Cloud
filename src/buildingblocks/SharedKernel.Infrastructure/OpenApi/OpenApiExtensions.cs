using System.Runtime.InteropServices;
using FastEndpoints.Swagger;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Common;
using Microsoft.AspNetCore.Builder;
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
                        setting.DocumentName = $"{appOptions.Name.ToLowerInvariant().Replace(" ", "-", StringComparison.InvariantCulture)}-v{apiVersion}";
                        setting.Description = appOptions.Description;

                        if (keycloakOptions != null)
                        {
                            // TODO: Fix security scheme configuration - needs proper FastEndpoints API
                            // setting.SecuritySchemes.Add("oAuth2", AddOAuthScheme(keycloakOptions.KeycloakTokenEndpoint, keycloakOptions.KeycloakUrlRealm + "protocol/openid-connect/auth", keycloakOptions.KeycloakTokenEndpoint));
                        }

                        // setting.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("oAuth2"));
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
                    options.AddDocument($"{appOptions.Name.ToLowerInvariant().Replace(" ", "-", StringComparison.InvariantCulture)}-v{apiVersion}");
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
    }
}
