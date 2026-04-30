using Basket.Api.Endpoints.V1.Basket.AddItem;
using Basket.Application;
using Catalog.Api.Endpoints.V1.Categories;
using Catalog.Application;
using Customer.Api.Endpoints.V1.Tenants.GetTenantById;
using Customer.Application;
using Order.Application;
using Order.Api.Endpoints.V1.Orders.CreateFromBasket;
using System.Text.RegularExpressions;
using Xunit;
using Assembly = System.Reflection.Assembly;

namespace Teck.Cloud.Arch.Tests;

public class ApiThinSliceBoundaryTests
{
    private static readonly Assembly BasketApplicationAssembly = typeof(IBasketApplication).Assembly;
    private static readonly Assembly BasketApiAssembly = typeof(AddBasketItemEndpoint).Assembly;
    private static readonly Assembly CatalogApplicationAssembly = typeof(ICatalogApplication).Assembly;
    private static readonly Assembly CatalogApiAssembly = typeof(GetCategoryByIdEndpoint).Assembly;
    private static readonly Assembly CustomerApplicationAssembly = typeof(ICustomerApplication).Assembly;
    private static readonly Assembly CustomerApiAssembly = typeof(GetTenantByIdEndpoint).Assembly;
    private static readonly Assembly OrderApplicationAssembly = typeof(IOrderApplication).Assembly;
    private static readonly Assembly OrderApiAssembly = typeof(CreateOrderFromBasketEndpoint).Assembly;

    [Fact]
    public void BasketApi_ShouldNotContain_RequestOrValidatorTypes()
    {
        IReadOnlySet<string> actual = GetRequestOrValidatorTypeNames(BasketApiAssembly, "Basket.Api");
        Assert.Empty(actual);
    }

    [Fact]
    public void OrderApi_ShouldNotContain_RequestOrValidatorTypes()
    {
        IReadOnlySet<string> actual = GetRequestOrValidatorTypeNames(OrderApiAssembly, "Order.Api");
        Assert.Empty(actual);
    }

    [Fact]
    public void CatalogApi_ShouldNotContain_RequestOrValidatorTypes()
    {
        IReadOnlySet<string> actual = GetRequestOrValidatorTypeNames(CatalogApiAssembly, "Catalog.Api");
        Assert.Empty(actual);
    }

    [Fact]
    public void CustomerApi_ShouldNotContain_RequestOrValidatorTypes()
    {
        IReadOnlySet<string> actual = GetRequestOrValidatorTypeNames(CustomerApiAssembly, "Customer.Api");
        Assert.Empty(actual);
    }

    [Fact]
    public void BasketApplication_ShouldNotContain_EndpointTypes()
    {
        IReadOnlySet<string> actual = GetEndpointTypeNames(BasketApplicationAssembly, "Basket.Application");
        Assert.Empty(actual);
    }

    [Fact]
    public void OrderApplication_ShouldNotContain_EndpointTypes()
    {
        IReadOnlySet<string> actual = GetEndpointTypeNames(OrderApplicationAssembly, "Order.Application");
        Assert.Empty(actual);
    }

    [Fact]
    public void CatalogApplication_ShouldNotContain_EndpointTypes()
    {
        IReadOnlySet<string> actual = GetEndpointTypeNames(CatalogApplicationAssembly, "Catalog.Application");
        Assert.Empty(actual);
    }

    [Fact]
    public void CustomerApplication_ShouldNotContain_EndpointTypes()
    {
        IReadOnlySet<string> actual = GetEndpointTypeNames(CustomerApplicationAssembly, "Customer.Application");
        Assert.Empty(actual);
    }

    [Fact]
    public void ApplicationProjects_ShouldNotContain_RootFeaturesNamespaceTypes()
    {
        Assert.Empty(GetRootFeaturesNamespaceTypeNames(BasketApplicationAssembly, "Basket.Application"));
        Assert.Empty(GetRootFeaturesNamespaceTypeNames(OrderApplicationAssembly, "Order.Application"));
        Assert.Empty(GetRootFeaturesNamespaceTypeNames(CatalogApplicationAssembly, "Catalog.Application"));
        Assert.Empty(GetRootFeaturesNamespaceTypeNames(CustomerApplicationAssembly, "Customer.Application"));
    }

    [Fact]
    public void ApplicationProjects_RequestAndValidatorTypes_ShouldUse_VersionedFeatureNamespaces()
    {
        Assert.Empty(GetRequestOrValidatorOutsideVersionedFeatures(BasketApplicationAssembly, "Basket.Application"));
        Assert.Empty(GetRequestOrValidatorOutsideVersionedFeatures(OrderApplicationAssembly, "Order.Application"));
        Assert.Empty(GetRequestOrValidatorOutsideVersionedFeatures(CatalogApplicationAssembly, "Catalog.Application"));
        Assert.Empty(GetRequestOrValidatorOutsideVersionedFeatures(CustomerApplicationAssembly, "Customer.Application"));
    }

    private static IReadOnlySet<string> GetRequestOrValidatorTypeNames(Assembly assembly, string rootNamespace)
    {
        return assembly
            .GetTypes()
            .Where(type => type.Namespace is { } ns && ns.StartsWith(rootNamespace, StringComparison.Ordinal))
            .Where(type => type.Name.EndsWith("Request", StringComparison.Ordinal) || type.Name.EndsWith("Validator", StringComparison.Ordinal))
            .Select(type => type.FullName ?? type.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlySet<string> GetEndpointTypeNames(Assembly assembly, string rootNamespace)
    {
        return assembly
            .GetTypes()
            .Where(type => type.Namespace is { } ns && ns.StartsWith(rootNamespace, StringComparison.Ordinal))
            .Where(type => type.Name.EndsWith("Endpoint", StringComparison.Ordinal))
            .Select(type => type.FullName ?? type.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlySet<string> GetRootFeaturesNamespaceTypeNames(Assembly assembly, string rootNamespace)
    {
        string disallowedPrefix = $"{rootNamespace}.Features.";

        return assembly
            .GetTypes()
            .Where(type => type.Namespace is { } ns && ns.StartsWith(disallowedPrefix, StringComparison.Ordinal))
            .Select(type => type.FullName ?? type.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlySet<string> GetRequestOrValidatorOutsideVersionedFeatures(Assembly assembly, string rootNamespace)
    {
        Regex versionedFeatureNamespacePattern = new(@"\.Features\.[^.]+\.V\d+(?:\.|$)", RegexOptions.Compiled);

        return assembly
            .GetTypes()
            .Where(type => type.Namespace is { } ns && ns.StartsWith(rootNamespace, StringComparison.Ordinal))
            .Where(type => type.Name.EndsWith("Request", StringComparison.Ordinal) || type.Name.EndsWith("Validator", StringComparison.Ordinal))
            .Where(type => type.Namespace is not null && type.Namespace.Contains(".Features.", StringComparison.Ordinal))
            .Where(type => type.Namespace is not null && !versionedFeatureNamespacePattern.IsMatch(type.Namespace))
            .Select(type => type.FullName ?? type.Name)
            .ToHashSet(StringComparer.Ordinal);
    }
}
