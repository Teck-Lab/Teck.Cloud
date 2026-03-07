using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Yarp.ReverseProxy.Configuration;

namespace Web.Edge.UnitTests.Services;

public sealed class EdgeGatewayHelpersPolicyTests
{
    private const string NoTenantPolicy = "<none>";

    [Theory]
    [MemberData(nameof(EmployeeOnlyMetadataCases))]
    public void IsEmployeeOnlyRoute_ShouldRespectMetadataCases(string policyValue, string path, bool expected)
    {
        RouteConfig routeConfig = CreateRouteConfig(new Dictionary<string, string>
        {
            ["EdgeAccessPolicy"] = policyValue,
        });

        bool result = InvokeIsEmployeeOnlyRoute(routeConfig, path, "admin");

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("/catalog/admin", true)]
    [InlineData("/catalog/v1/admin/products", true)]
    [InlineData("/catalog/ADMIN/products", true)]
    [InlineData("/catalog/myadministrator/products", false)]
    [InlineData("/catalog/super-admin-area", false)]
    public void IsEmployeeOnlyRoute_ShouldUseSegmentAwareFallback(string path, bool expected)
    {
        RouteConfig routeConfig = CreateRouteConfig();

        bool result = InvokeIsEmployeeOnlyRoute(routeConfig, path, "admin");

        result.ShouldBe(expected);
    }

    [Theory]
    [MemberData(nameof(TenantSkipCases))]
    public void ShouldSkipTenantResolution_ShouldRespectPolicyAndFallback(string tenantPolicy, string path, bool expected)
    {
        Dictionary<string, string>? metadata = string.Equals(tenantPolicy, NoTenantPolicy, StringComparison.Ordinal)
            ? null
            : new Dictionary<string, string>
            {
                ["EdgeTenantPolicy"] = tenantPolicy,
            };

        RouteConfig routeConfig = CreateRouteConfig(metadata);

        bool result = InvokeShouldSkipTenantResolution(routeConfig, path, "admin");

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

    private static bool InvokeIsEmployeeOnlyRoute(RouteConfig routeConfig, string path, string adminPathSegment)
    {
        Type helperType = GetEdgeGatewayHelpersType();
        return (bool)helperType
            .GetMethod("IsEmployeeOnlyRoute")!
            .Invoke(null, [routeConfig, new PathString(path), adminPathSegment])!;
    }

    private static bool InvokeShouldSkipTenantResolution(RouteConfig routeConfig, string path, string adminPathSegment)
    {
        Type helperType = GetEdgeGatewayHelpersType();
        return (bool)helperType
            .GetMethod("ShouldSkipTenantResolution")!
            .Invoke(null, [routeConfig, new PathString(path), adminPathSegment])!;
    }

    private static object InvokeGetEdgeRouteSecurityOptions(IConfiguration configuration)
    {
        Type extensionsType = Type.GetType("Web.Edge.Services.EdgeRouteSecurityOptionsExtensions, Web.Edge", throwOnError: true)!;
        return extensionsType
            .GetMethod("GetEdgeRouteSecurityOptions")!
            .Invoke(null, [configuration])!;
    }

    private static Type GetEdgeGatewayHelpersType()
    {
        return Type.GetType("Web.Edge.Services.EdgeGatewayHelpers, Web.Edge", throwOnError: true)!;
    }

    public static IEnumerable<object[]> EmployeeOnlyMetadataCases()
    {
        yield return ["EmployeeOnly", "/catalog/v1/products", true];
        yield return ["AdminOnly", "/catalog/v1/products", true];
        yield return ["Public", "/catalog/admin/products", false];
    }

    public static IEnumerable<object[]> TenantSkipCases()
    {
        yield return new object[] { NoTenantPolicy, "/catalog/v1/products", false };
        yield return new object[] { NoTenantPolicy, "/catalog/admin/products", true };
        yield return new object[] { "Required", "/catalog/admin/products", false };
        yield return new object[] { "None", "/catalog/v1/products", true };
    }
}
