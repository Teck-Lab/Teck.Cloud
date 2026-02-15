using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Text.Json;
using Keycloak.AuthServices.Authentication;
using Scalar.AspNetCore;
using SharedKernel.Infrastructure.Auth;
using Web.Edge.Middleware;
using Web.Edge.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var keycloakSection = builder.Configuration.GetSection("Keycloak");
var keycloakOptions = new KeycloakAuthenticationOptions();
keycloakSection.Bind(keycloakOptions);

builder.Services.AddKeycloak(builder.Configuration, builder.Environment, keycloakOptions);
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();
builder.Services.AddSingleton<IInternalIdentityTokenService, InternalIdentityTokenService>();

builder.Services.AddAuthentication();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RealmAdminOnly", policy =>
        policy.RequireAssertion(context => IsRealmAdmin(context.User)));
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("ok"));

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<EdgeRequestSanitizationMiddleware>();

app.MapScalarApiReference("/docs", options =>
{
    options.WithTitle("Teck Web API");

    var docs = GetScalarDocuments(builder.Configuration, includeAdminRoutes: false);
    var isFirst = true;
    foreach (var doc in docs)
    {
        options.AddDocument(doc.DocumentName, doc.DisplayName, doc.OpenApiPath, isDefault: isFirst);
        isFirst = false;
    }

    options
        .AddPreferredSecuritySchemes("oAuth2")
        .AddAuthorizationCodeFlow("oAuth2", flow =>
        {
            flow.ClientId = builder.Configuration["Keycloak:ScalarClientId"] ?? "scalar-ui";
            flow.Pkce = Pkce.Sha256;
        });
});

app.MapScalarApiReference("/docs/admin", options =>
{
    options.WithTitle("Teck Internal APIs");

    var docs = GetScalarDocuments(builder.Configuration, includeAdminRoutes: true);
    var isFirst = true;
    foreach (var doc in docs)
    {
        options.AddDocument(doc.DocumentName, doc.DisplayName, doc.OpenApiPath, isDefault: isFirst);
        isFirst = false;
    }

    options
        .AddPreferredSecuritySchemes("oAuth2")
        .AddAuthorizationCodeFlow("oAuth2", flow =>
        {
            flow.ClientId = builder.Configuration["Keycloak:ScalarClientId"] ?? "scalar-ui";
            flow.Pkce = Pkce.Sha256;
        });
});

app.MapReverseProxy();

app.Run();

static List<(string DocumentName, string DisplayName, string OpenApiPath)> GetScalarDocuments(IConfiguration configuration, bool includeAdminRoutes)
{
    var docs = new List<(string DocumentName, string DisplayName, string OpenApiPath)>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var cluster in configuration.GetSection("ReverseProxy:Clusters").GetChildren())
    {
        var clusterName = cluster.Key;
        foreach (var destination in cluster.GetSection("Destinations").GetChildren())
        {
            foreach (var swagger in destination.GetSection("Swaggers").GetChildren())
            {
                var prefixPath = swagger["PrefixPath"];
                var serviceName = !string.IsNullOrWhiteSpace(prefixPath)
                    ? prefixPath.Trim('/').ToLowerInvariant()
                    : clusterName.ToLowerInvariant();

                foreach (var pathItem in swagger.GetSection("Paths").GetChildren())
                {
                    var openApiPath = pathItem.Value;
                    if (string.IsNullOrWhiteSpace(openApiPath))
                    {
                        continue;
                    }

                    var isAdminDoc = openApiPath.Contains("/openapi/admin/", StringComparison.OrdinalIgnoreCase);
                    if (!includeAdminRoutes && isAdminDoc)
                    {
                        continue;
                    }

                    var versionMatch = Regex.Match(openApiPath, @"v\d+", RegexOptions.IgnoreCase);
                    var version = versionMatch.Success ? versionMatch.Value.ToLowerInvariant() : "v1";
                    var documentName = $"{serviceName}-{version}";

                    var displayServiceName = Regex.Replace(serviceName, "[-_]+", " ");
                    if (!string.IsNullOrEmpty(displayServiceName))
                    {
                        displayServiceName = char.ToUpperInvariant(displayServiceName[0]) + displayServiceName[1..];
                    }

                    var displayName = $"{displayServiceName} {version}";
                    if (seen.Add(documentName))
                    {
                        docs.Add((documentName, displayName, openApiPath));
                    }
                }
            }
        }
    }

    if (docs.Count == 0)
    {
        docs.Add(("bff-v1", "Bff v1", "/openapi/bff/v1/openapi.json"));
    }

    return docs;
}

static bool IsRealmAdmin(ClaimsPrincipal user)
{
    if (user.IsInRole("realm-admin"))
    {
        return true;
    }

    foreach (var claim in user.Claims)
    {
        if ((string.Equals(claim.Type, "roles", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(claim.Type, "role", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(claim.Type, "realm_access.roles", StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(claim.Value, "realm-admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(claim.Type, "realm_access", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(claim.Value) &&
            claim.Value.TrimStart().StartsWith('{'))
        {
            try
            {
                using var jsonDocument = JsonDocument.Parse(claim.Value);
                if (jsonDocument.RootElement.TryGetProperty("roles", out var rolesElement) &&
                    rolesElement.ValueKind == JsonValueKind.Array &&
                    rolesElement.EnumerateArray().Any(role =>
                        role.ValueKind == JsonValueKind.String &&
                        string.Equals(role.GetString(), "realm-admin", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore malformed claim payloads and continue evaluating remaining claims.
            }
        }
    }

    return false;
}
