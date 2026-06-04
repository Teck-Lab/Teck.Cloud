using Microsoft.Extensions.Configuration;
using Shouldly;
using Yarp.ReverseProxy.Configuration;

namespace Web.Public.Gateway.UnitTests.Services;

public sealed class EdgeGatewayHelpersPolicyTests
{
    private const string NoTenantPolicy = "<none>";

    [Theory]
    [MemberData(nameof(TenantSkipCases))]
    public void ShouldSkipTenantResolution_ShouldRespectPolicyAndFallback(string tenantPolicy, bool expected)
    {
        Dictionary<string, string>? metadata = string.Equals(tenantPolicy, NoTenantPolicy, StringComparison.Ordinal)
            ? null
            : new Dictionary<string, string>
            {
                ["EdgeTenantPolicy"] = tenantPolicy,
            };

        RouteConfig routeConfig = CreateRouteConfig(metadata);

        bool result = InvokeShouldSkipTenantResolution(routeConfig);

        result.ShouldBe(expected);
    }

    [Fact]
    public void GetEdgeRouteSecurityOptions_ShouldFallbackToLegacyKeys()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EdgeRouteSecurity:Enabled"] = "true",
                ["EdgeRouteSecurity:AdminPathKeyword"] = "admin",
                ["EdgeRouteSecurity:AdminRole"] = "employee",
            })
            .Build();

        object options = InvokeGetEdgeRouteSecurityOptions(configuration);

        options.GetType().GetProperty("Enabled")!.GetValue(options).ShouldBe(true);
        options.GetType().GetProperty("AdminPathSegment")!.GetValue(options).ShouldBe("admin");
        options.GetType().GetProperty("EmployeeRole")!.GetValue(options).ShouldBe("employee");
    }

    private static RouteConfig CreateRouteConfig(Dictionary<string, string>? metadata = null)
    {
        return new RouteConfig
        {
            RouteId = "test",
            ClusterId = "catalog",
            Match = new RouteMatch { Path = "/{**catch-all}" },
            Metadata = metadata,
        };
    }

    private static bool InvokeShouldSkipTenantResolution(RouteConfig routeConfig)
    {
        Type helperType = GetEdgeGatewayHelpersType();
        return (bool)helperType
            .GetMethod("ShouldSkipTenantResolution")!
            .Invoke(null, [routeConfig])!;
    }

    private static object InvokeGetEdgeRouteSecurityOptions(IConfiguration configuration)
    {
        Type extensionsType = Type.GetType("Web.Public.Gateway.Services.EdgeRouteSecurityOptionsExtensions, Web.Public.Gateway", throwOnError: true)!;
        return extensionsType
            .GetMethod("GetEdgeRouteSecurityOptions")!
            .Invoke(null, [configuration])!;
    }

    private static Type GetEdgeGatewayHelpersType()
    {
        return Type.GetType("Web.Public.Gateway.Services.EdgeGatewayHelpers, Web.Public.Gateway", throwOnError: true)!;
    }

    public static IEnumerable<object[]> TenantSkipCases()
    {
        yield return new object[] { NoTenantPolicy, false };
        yield return new object[] { "Required", false };
        yield return new object[] { "None", true };
    }
}
