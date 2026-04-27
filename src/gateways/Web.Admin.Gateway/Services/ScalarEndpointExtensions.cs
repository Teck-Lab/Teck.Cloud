using Scalar.AspNetCore;

namespace Web.Admin.Gateway.Services;

internal static class ScalarEndpointExtensions
{
    public static void MapAdminScalarApiReference(this WebApplication app, IConfiguration configuration)
    {
        app.MapScalarApiReference("docs", options =>
            {
                options.WithOpenApiRoutePattern("/openapi/{documentName}/openapi.json");
                var addedDocuments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

                            foreach (string path in swagger
                                .GetSection("Paths")
                                .GetChildren()
                                .Select(pathNode => pathNode.Value)
                                .Where(static path => !string.IsNullOrWhiteSpace(path))
                                .Cast<string>())
                            {
                                if (!TryResolveDocumentName(path, out string resolvedDocumentName))
                                {
                                    continue;
                                }

                                string normalizedDocumentName = resolvedDocumentName.ToLowerInvariant();
                                bool isAdminDocument = normalizedDocumentName.Equals("admin", StringComparison.OrdinalIgnoreCase);
                                string documentName = isAdminDocument
                                    ? $"{normalizedService}-admin"
                                    : normalizedService;
                                if (!addedDocuments.Add(documentName))
                                {
                                    continue;
                                }

                                string documentTitle = isAdminDocument
                                    ? $"{GetClusterDisplayName(cluster.Key)} Admin"
                                    : GetClusterDisplayName(cluster.Key);

                                options.AddDocument(
                                    documentName,
                                    documentTitle,
                                    $"/{normalizedService}/openapi/{resolvedDocumentName}/openapi.json");
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
            })
            .RequireAuthorization("PlatformAdmin");
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

    private static bool TryResolveDocumentName(string path, out string documentName)
    {
        documentName = string.Empty;

        string[] segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (int index = 0; index < segments.Length - 2; index++)
        {
            if (!segments[index].Equals("openapi", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!segments[index + 2].Equals("openapi.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string candidate = segments[index + 1];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            documentName = candidate;
            return true;
        }

        return false;
    }

    private static string GetClusterDisplayName(string? clusterKey)
    {
        if (string.IsNullOrWhiteSpace(clusterKey))
        {
            return "Service";
        }

        string trimmed = clusterKey.Trim();
        return $"{char.ToUpperInvariant(trimmed[0])}{trimmed[1..].ToLowerInvariant()}";
    }
}
