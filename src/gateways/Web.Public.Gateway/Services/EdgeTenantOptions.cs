namespace Web.Public.Gateway.Services;

internal sealed record EdgeTenantOptions(
    string TenantIdHeaderName,
    string OrganizationClaimName,
    string TenantIdClaimName);

internal static class EdgeTenantOptionsExtensions
{
    public static EdgeTenantOptions GetEdgeTenantOptions(this IConfiguration configuration)
    {
        return new EdgeTenantOptions(
            TenantIdHeaderName: configuration["MultiTenancy:TenantIdHeaderName"] ?? "X-TenantId",
            OrganizationClaimName: configuration["MultiTenancy:OrganizationClaimName"] ?? "organization",
            TenantIdClaimName: configuration["MultiTenancy:TenantIdClaimName"] ?? "tenant_id");
    }
}
