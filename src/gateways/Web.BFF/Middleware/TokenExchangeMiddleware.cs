using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Web.BFF.Services;
using Yarp.ReverseProxy.Configuration;

namespace Web.BFF.Middleware
{
    public class TokenExchangeMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ITokenExchangeService _exchangeService;

        public TokenExchangeMiddleware(RequestDelegate next, ITokenExchangeService exchangeService)
        {
            _next = next;
            _exchangeService = exchangeService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            string? audience = null;
            if (endpoint != null)
            {
                var routeMetadata = endpoint.Metadata.GetMetadata<RouteConfig>();
                audience = routeMetadata?.Metadata?.GetValueOrDefault("KeycloakAudience") as string;
            }

            var auth = context.Request.Headers["Authorization"].FirstOrDefault();
            string? subjectToken = null;
            if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer "))
            {
                subjectToken = auth.Substring("Bearer ".Length).Trim();
            }

            var tenantId = ResolveTenantId(context.User);

            if (!string.IsNullOrEmpty(subjectToken) && !string.IsNullOrEmpty(audience))
            {
                try
                {
                    var token = await _exchangeService.ExchangeTokenAsync(subjectToken, audience, tenantId ?? "", context.RequestAborted);
                    context.Request.Headers["Authorization"] = "Bearer " + token.AccessToken;
                }
                catch
                {
                    // If token exchange fails, do not block here; let downstream handle unauthorized
                }
            }

            if (!string.IsNullOrEmpty(tenantId))
            {
                context.Request.Headers["X-TenantId"] = tenantId;
            }

            await _next(context);
        }

        private string? ResolveTenantId(ClaimsPrincipal user)
        {
            if (user == null) return null;

            var active = user.FindFirst("active_organization")?.Value;
            if (!string.IsNullOrEmpty(active))
            {
                try
                {
                    var doc = System.Text.Json.JsonDocument.Parse(active);
                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                    {
                        return idProp.GetString();
                    }
                }
                catch { }
            }

            var orgs = user.FindFirst("organizations")?.Value;
            if (!string.IsNullOrEmpty(orgs))
            {
                try
                {
                    var doc = System.Text.Json.JsonDocument.Parse(orgs);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        return prop.Name;
                    }
                }
                catch { }
            }

            var t = user.FindFirst("tenant_id")?.Value ?? user.FindFirst("tenant")?.Value;
            if (!string.IsNullOrEmpty(t)) return t;

            return null;
        }
    }
}
