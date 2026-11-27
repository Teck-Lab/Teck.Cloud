using SharedKernel.Core.MultiTenancy;
using LicensingHostApi = Teck.Cloud.Commerce.Licensing.Host.LicensingHost;
using OrdersHostApi = Teck.Cloud.Commerce.Orders.Host.OrdersHost;
using OrdersV1 = Teck.Cloud.Commerce.Orders.V1;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>The approved static startup verification inputs and test-only negative-control selection.</summary>
internal static class CommerceStaticCase
{
    internal const string CaseVariable = "COMMERCE_STATIC_NEGATIVE_CASE";
    internal const string HostVariable = "COMMERCE_STATIC_NEGATIVE_HOST";
    internal const string RootVariable = "TECK_COMMERCE_STATIC_ROOT";
    internal const string PristineRootVariable = "TECK_COMMERCE_STATIC_PRISTINE_ROOT";

    internal const string RegistryKind = "registry";
    internal const string AdapterKind = "adapter";

    internal const string RegistryGuardPrefix = "Commerce static Wolverine registry invalid:";
    internal const string RegistryLogPrefix = "Using pre-generated Wolverine HandlerRegistry (";
    internal const string RegistryFallbackMessage = "no pre-generated HandlerRegistry";

    internal const string Development = "Development";
    internal const string Production = "Production";

    internal static readonly CommerceStaticHost Orders = new(
        "Orders",
        "src/services/commerce/orders/Orders.Host/Internal/Generated",
        "CheckCurrentEntitlementHandler",
        typeof(OrdersHostApi),
        [typeof(OperationContext), typeof(ITenantDirectory)]);

    internal static readonly CommerceStaticHost Licensing = new(
        "Licensing",
        "src/services/commerce/licensing/Licensing.Host/Internal/Generated",
        "GetCurrentLicenseHandler",
        typeof(LicensingHostApi),
        [typeof(OperationContext), typeof(ITenantDirectory), typeof(OrdersV1.Orders.OrdersClient)]);

    /// <summary>Reads the explicitly requested hosting profile; unset means the framework default.</summary>
    /// <returns>The hosting environment every real commerce host must report.</returns>
    /// <exception cref="InvalidOperationException">The requested profiles disagree or are not approved.</exception>
    internal static string ExpectedProfile()
    {
        var dotnet = TrimmedEnvironment("DOTNET_ENVIRONMENT");
        var aspnet = TrimmedEnvironment("ASPNETCORE_ENVIRONMENT");
        if (dotnet is not null && aspnet is not null && !string.Equals(dotnet, aspnet, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "DOTNET_ENVIRONMENT and ASPNETCORE_ENVIRONMENT disagree: '" + dotnet + "' versus '" + aspnet + "'.");
        }

        var requested = aspnet ?? dotnet;
        return requested switch
        {
            null => Production,
            _ when string.Equals(requested, Development, StringComparison.Ordinal) => Development,
            _ when string.Equals(requested, Production, StringComparison.Ordinal) => Production,
            _ => throw new InvalidOperationException(
                "The requested commerce hosting profile must be Development or Production; observed '" + requested + "'."),
        };
    }

    /// <summary>Reads the explicit test-only negative-control selection; null means the positive run.</summary>
    /// <returns>The selected corruption case, host and source roots.</returns>
    /// <exception cref="InvalidOperationException">The selection is incomplete or not approved.</exception>
    internal static NegativeSelection? ReadNegative()
    {
        var kind = TrimmedEnvironment(CaseVariable);
        if (kind is null)
        {
            return null;
        }

        if (!string.Equals(kind, RegistryKind, StringComparison.Ordinal)
            && !string.Equals(kind, AdapterKind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                CaseVariable + " must be '" + RegistryKind + "', '" + AdapterKind + "' or unset; observed '" + kind + "'.");
        }

        var host = Host(TrimmedEnvironment(HostVariable)
            ?? throw new InvalidOperationException(HostVariable + " is required when " + CaseVariable + " is set."));
        return new NegativeSelection(
            kind,
            host,
            AbsoluteRoot(RootVariable),
            AbsoluteRoot(PristineRootVariable));
    }

    internal static CommerceStaticHost Host(string name) => name switch
    {
        "Orders" => Orders,
        "Licensing" => Licensing,
        _ => throw new InvalidOperationException(
            HostVariable + " must be 'Orders' or 'Licensing'; observed '" + name + "'."),
    };

    internal static CommerceStaticHost Other(CommerceStaticHost host) =>
        ReferenceEquals(host, Orders) ? Licensing : Orders;

    private static string? TrimmedEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string AbsoluteRoot(string name)
    {
        var value = TrimmedEnvironment(name)
            ?? throw new InvalidOperationException(name + " is required for the test-only negative-control run.");
        return Path.IsPathRooted(value)
            ? value
            : throw new InvalidOperationException(name + " must be an absolute path; observed '" + value + "'.");
    }
}
