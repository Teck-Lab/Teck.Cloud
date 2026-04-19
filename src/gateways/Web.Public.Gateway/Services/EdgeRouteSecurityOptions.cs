using System.Diagnostics.CodeAnalysis;

namespace Web.Public.Gateway.Services;

internal sealed record EdgeRouteSecurityOptions(
    bool Enabled,
    string AdminPathSegment,
    string EmployeeRole);

internal static class EdgeRouteSecurityOptionsExtensions
{
    [RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.GetValue<T>(String)")]
    public static EdgeRouteSecurityOptions GetEdgeRouteSecurityOptions(this IConfiguration configuration)
    {
        return new EdgeRouteSecurityOptions(
            Enabled: configuration.GetValue<bool?>("EdgeRouteSecurity:Enabled") ?? true,
            AdminPathSegment: configuration["EdgeRouteSecurity:AdminPathSegment"]
                ?? configuration["EdgeRouteSecurity:AdminPathKeyword"]
                ?? "admin",
            EmployeeRole: configuration["EdgeRouteSecurity:EmployeeRole"]
                ?? configuration["EdgeRouteSecurity:AdminRole"]
                ?? "employee");
    }
}
