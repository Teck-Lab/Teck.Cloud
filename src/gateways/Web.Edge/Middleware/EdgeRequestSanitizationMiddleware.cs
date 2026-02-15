namespace Web.Edge.Middleware;

using System.Security.Claims;
using Web.Edge.Security;

public sealed class EdgeRequestSanitizationMiddleware
{
    private static readonly string[] HeadersToStrip =
    {
        "X-TenantId",
        "X-Tenant-DbStrategy",
        "X-Internal-Identity",
        "X-Forwarded-User",
        "X-Forwarded-Roles",
        "X-Forwarded-Tenant"
    };

    private readonly RequestDelegate _next;
    private readonly IInternalIdentityTokenService _internalIdentityTokenService;

    public EdgeRequestSanitizationMiddleware(
        RequestDelegate next,
        IInternalIdentityTokenService internalIdentityTokenService)
    {
        _next = next;
        _internalIdentityTokenService = internalIdentityTokenService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        foreach (var header in HeadersToStrip)
        {
            context.Request.Headers.Remove(header);
        }

        if (context.Request.Path.StartsWithSegments("/openapi/admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.Headers.Remove("Authorization");
        }

        var principal = context.User?.Identity?.IsAuthenticated == true
            ? context.User
            : CreateServicePrincipal();

        var internalIdentityToken = _internalIdentityTokenService.CreateToken(principal);
        if (!string.IsNullOrWhiteSpace(internalIdentityToken))
        {
            context.Request.Headers["X-Internal-Identity"] = internalIdentityToken;
        }

        await _next(context);
    }

    private static ClaimsPrincipal CreateServicePrincipal()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "web-edge"),
            new Claim("client_id", "web-edge")
        ],
        authenticationType: "EdgeInternal");

        return new ClaimsPrincipal(identity);
    }
}
