using Scalar.AspNetCore;
using System.Text.RegularExpressions;

namespace Web.Edge.Services;

internal static class ScalarEndpointExtensions
{
    public static void MapEdgeScalarApiReference(this WebApplication app, IConfiguration configuration)
    {
        app.MapScalarApiReference("docs", options =>
        {
            options.WithOpenApiRoutePattern("/openapi/{documentName}/openapi.json");

            IConfigurationSection clustersSection = configuration.GetSection("ReverseProxy:Clusters");
            foreach (IConfigurationSection cluster in clustersSection.GetChildren())
            {
                foreach (IConfigurationSection destination in cluster.GetSection("Destinations").GetChildren())
                {
                    IConfigurationSection swaggersSection = destination.GetSection("Swaggers");
                    foreach (IConfigurationSection swagger in swaggersSection.GetChildren())
                    {
                        string? prefixPath = swagger["PrefixPath"];
                        if (string.IsNullOrWhiteSpace(prefixPath))
                        {
                            continue;
                        }

                        string service = prefixPath.Trim('/');
                        string normalizedService = service.ToLowerInvariant();

                        foreach (IConfigurationSection pathNode in swagger.GetSection("Paths").GetChildren())
                        {
                            string? path = pathNode.Value;
                            if (string.IsNullOrWhiteSpace(path))
                            {
                                continue;
                            }

                            Match versionMatch = Regex.Match(path, @"v\d+");
                            string version = versionMatch.Success ? versionMatch.Value : "v1";
                            string normalizedVersion = version.ToLowerInvariant();
                            string documentName = $"{normalizedService}-{normalizedVersion}";

                            options.AddDocument(
                                documentName,
                                $"{cluster.Key[0].ToString().ToUpperInvariant()}{cluster.Key.Substring(1).ToLowerInvariant()} {version}",
                                $"/{normalizedService}/openapi/{normalizedVersion}/openapi.json");
                        }
                    }
                }
            }

            options
                .AddPreferredSecuritySchemes("oAuth2")
                .AddAuthorizationCodeFlow("oAuth2", flow =>
                {
                    flow.ClientId = configuration["Keycloak:ScalarClientId"] ?? "scalar-ui";
                    flow.Pkce = Pkce.Sha256;

                    if (TryGetKeycloakOidcEndpoints(configuration, out string? authorizationEndpoint, out string? tokenEndpoint))
                    {
                        flow.AuthorizationUrl = authorizationEndpoint;
                        flow.TokenUrl = tokenEndpoint;
                    }
                });
        });
    }

    private static bool TryGetKeycloakOidcEndpoints(
        IConfiguration configuration,
        out string? authorizationEndpoint,
        out string? tokenEndpoint)
    {
        authorizationEndpoint = null;
        tokenEndpoint = null;

        string? configuredAuthorizationEndpoint = configuration["Keycloak:AuthorizationEndpoint"];
        string? configuredTokenEndpoint = configuration["Keycloak:TokenEndpoint"];

        if (!string.IsNullOrWhiteSpace(configuredAuthorizationEndpoint) &&
            !string.IsNullOrWhiteSpace(configuredTokenEndpoint))
        {
            authorizationEndpoint = configuredAuthorizationEndpoint;
            tokenEndpoint = configuredTokenEndpoint;

            return true;
        }

        string? authServerUrl = configuration["Keycloak:auth-server-url"];
        string? realm = configuration["Keycloak:realm"];
        if (string.IsNullOrWhiteSpace(authServerUrl) || string.IsNullOrWhiteSpace(realm))
        {
            return false;
        }

        string realmBase = $"{authServerUrl.TrimEnd('/')}/realms/{realm.Trim('/')}";
        authorizationEndpoint = $"{realmBase}/protocol/openid-connect/auth";
        tokenEndpoint = $"{realmBase}/protocol/openid-connect/token";

        return true;
    }
}
