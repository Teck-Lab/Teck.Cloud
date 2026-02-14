using System.Security.Claims;
using System.Text.Json;
using Keycloak.AuthServices.Authentication;
using Scalar.AspNetCore;
using SharedKernel.Infrastructure.Auth;
using Web.Edge.Middleware;
using Web.Edge.Security;

var builder = WebApplication.CreateBuilder(args);

var keycloakSection = builder.Configuration.GetSection("Keycloak");
var keycloakOptions = new KeycloakAuthenticationOptions();
keycloakSection.Bind(keycloakOptions);

builder.Services.AddKeycloak(builder.Configuration, builder.Environment, keycloakOptions);
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
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
    options.WithTitle("Teck Web API")
        .AddDocument("bff", "Web BFF", "/openapi/bff/v1/openapi.json", isDefault: true);
}).AllowAnonymous();

app.MapScalarApiReference("/docs/admin", options =>
{
    options.WithTitle("Teck Internal APIs")
        .AddDocument("bff", "Web BFF", "/openapi/bff/v1/openapi.json", isDefault: true)
        .AddDocument("catalog", "Catalog API", "/openapi/admin/catalog/v1/openapi.json")
        .AddDocument("customer", "Customer API", "/openapi/admin/customer/v1/openapi.json");
}).RequireAuthorization("RealmAdminOnly");

app.MapReverseProxy();

app.Run();

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
