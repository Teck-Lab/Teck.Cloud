using System.Security.Claims;
using Web.BFF.Services;
using Yarp.ReverseProxy.Configuration;

namespace Web.BFF.Middleware
{
    public class TokenExchangeMiddleware
    {
        private const string TenantIdHeader = "X-TenantId";
        private const string TenantDbStrategyHeader = "X-Tenant-DbStrategy";
        private readonly RequestDelegate _next;
        private readonly ITokenExchangeService _exchangeService;
        private readonly ITenantRoutingMetadataService _tenantRoutingMetadataService;

        public TokenExchangeMiddleware(
            RequestDelegate next,
            ITokenExchangeService exchangeService,
            ITenantRoutingMetadataService tenantRoutingMetadataService)
        {
            _next = next;
            _exchangeService = exchangeService;
            _tenantRoutingMetadataService = tenantRoutingMetadataService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            string? audience = null;
            if (endpoint != null)
            {
                var routeMetadata = endpoint.Metadata.GetMetadata<RouteConfig>();
                audience = routeMetadata?.Metadata?.GetValueOrDefault("KeycloakAudience");
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
                context.Request.Headers[TenantIdHeader] = tenantId;

                try
                {
                    var routingMetadata = await _tenantRoutingMetadataService
                        .GetTenantRoutingMetadataAsync(tenantId, context.RequestAborted);

                    if (!string.IsNullOrWhiteSpace(routingMetadata?.DatabaseStrategy))
                    {
                        context.Request.Headers[TenantDbStrategyHeader] = routingMetadata.DatabaseStrategy;
                    }
                }
                catch
                {
                    // Metadata lookup is best-effort; downstream resolver fallback remains authoritative.
                }
            }

            await _next(context);
        }

        private string? ResolveTenantId(ClaimsPrincipal user)
        {
            if (user == null) return null;

            if (TryResolveTenantIdFromOrganizationClaim(user, "organization", out var tenantIdFromOrganizationClaim))
            {
                return tenantIdFromOrganizationClaim;
            }

            if (TryResolveTenantIdFromOrganizationClaim(user, "organizations", out var tenantIdFromOrganizationsClaim))
            {
                return tenantIdFromOrganizationsClaim;
            }

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

        private static bool TryResolveTenantIdFromOrganizationClaim(
            ClaimsPrincipal user,
            string claimName,
            out string? tenantId)
        {
            tenantId = null;

            var organizationClaim = user.FindFirst(claimName)?.Value;
            if (string.IsNullOrWhiteSpace(organizationClaim))
            {
                return false;
            }

            try
            {
                using var organizationsDocument = System.Text.Json.JsonDocument.Parse(organizationClaim);
                if (organizationsDocument.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    return false;
                }

                foreach (var organization in organizationsDocument.RootElement.EnumerateObject())
                {
                    if (organization.Value.ValueKind == System.Text.Json.JsonValueKind.Object &&
                        organization.Value.TryGetProperty("id", out var idProperty) &&
                        idProperty.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        tenantId = idProperty.GetString();
                        if (!string.IsNullOrWhiteSpace(tenantId))
                        {
                            return true;
                        }
                    }

                    if (organization.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        tenantId = organization.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(tenantId))
                        {
                            return true;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(organization.Name))
                    {
                        tenantId = organization.Name;
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }
}
