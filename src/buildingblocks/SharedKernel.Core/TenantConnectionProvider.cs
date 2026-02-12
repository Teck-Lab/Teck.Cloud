using Microsoft.Extensions.Configuration;

namespace SharedKernel.Core;

public static class TenantConnectionProvider
{
    public static string GetTenantConnection(IConfiguration config, string tenantId, bool readOnly = false)
    {
        var side = readOnly ? "Read" : "Write";
        var key = $"ConnectionStrings:Tenants:{tenantId}:{side}";
        var dsn = config[key] ?? Environment.GetEnvironmentVariable($"ConnectionStrings__Tenants__{tenantId}__{side}");
        if (string.IsNullOrWhiteSpace(dsn))
            throw new InvalidOperationException($"Tenant connection string not found for tenant '{tenantId}' (key: {key})");
        return dsn;
    }
}
