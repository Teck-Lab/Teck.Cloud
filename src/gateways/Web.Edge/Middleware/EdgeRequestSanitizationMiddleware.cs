namespace Web.Edge.Middleware;

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

        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var internalIdentityToken = _internalIdentityTokenService.CreateToken(context.User);
            if (!string.IsNullOrWhiteSpace(internalIdentityToken))
            {
                context.Request.Headers["X-Internal-Identity"] = internalIdentityToken;
            }
        }

        await _next(context);
    }
}
