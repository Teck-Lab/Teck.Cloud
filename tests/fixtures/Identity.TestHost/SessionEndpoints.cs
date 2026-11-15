using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.JsonWebTokens;
using SharedKernel.Infrastructure.Auth;

namespace Identity.TestHost;

internal static class SessionEndpoints
{
    internal static void Map(WebApplication app)
    {
        _ = app.MapGet("/proof/assurance", (HttpContext http) => Results.Json(new
        {
            Acr = http.User.FindAll("acr").Select(claim => claim.Value),
            AuthenticationTimes = http.User.FindAll("auth_time").Select(claim => claim.Value),
            AuthenticatedIdentities = http.User.Identities.Count(identity => identity.IsAuthenticated),
        })).RequireAuthorization();
        _ = app.MapGet("/proof/exchanged-assurance", async (IAccessTokenProvider tokens, CancellationToken cancellation) =>
        {
            // Diagnostic decoding only; the downstream JWT handler independently validates and authorizes it.
            var token = new JsonWebToken(await tokens.GetTokenAsync("identity-service", cancellation).ConfigureAwait(false));
            return Results.Json(token.Claims.Where(claim => new[] { "acr", "auth_time", "realm_access", "azp" }.Contains(claim.Type, StringComparer.Ordinal)).Select(claim => new { claim.Type, claim.Value }));
        }).RequireAuthorization();
        _ = app.MapPost("/session/refresh", async (HttpContext http, IAntiforgery antiforgery) =>
        {
            if (!await ValidateAsync(http, antiforgery).ConfigureAwait(false))
            {
                return Results.BadRequest();
            }

            var original = await http.GetTokenAsync("access_token").ConfigureAwait(false);
            var result = await http.GetUserAccessTokenAsync(new UserTokenRequestParameters { ForceTokenRenewal = true }, http.RequestAborted).ConfigureAwait(false);
            return result.Token is { } token
                ? Results.Json(!string.Equals(original, token.AccessToken.ToString(), StringComparison.Ordinal))
                : Results.Unauthorized();
        }).RequireAuthorization();
        _ = app.MapPost("/session/logout", async (HttpContext http, IAntiforgery antiforgery) =>
        {
            return await ValidateAsync(http, antiforgery).ConfigureAwait(false)
                ? Results.SignOut(
                    new AuthenticationProperties { RedirectUri = "/signed-out" },
                    [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme])
                : Results.BadRequest();
        }).RequireAuthorization();
        _ = app.MapGet("/signed-out", () => Results.Text("Signed out")).AllowAnonymous();
    }

    private static async Task<bool> ValidateAsync(HttpContext http, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(http).ConfigureAwait(false);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }
}
