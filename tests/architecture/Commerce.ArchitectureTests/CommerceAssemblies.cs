using System.Reflection;

namespace Commerce.ArchitectureTests;

/// <summary>Resolves the compiled commerce service and gateway assemblies under test.</summary>
internal static class CommerceAssemblies
{
    internal const string OrdersNamespace = "Teck.Cloud.Commerce.Orders";
    internal const string LicensingNamespace = "Teck.Cloud.Commerce.Licensing";

    internal static Assembly OrdersDomain { get; } = Load("Orders.Domain");

    internal static Assembly OrdersApplication { get; } = Load("Orders.Application");

    internal static Assembly OrdersInfrastructure { get; } = Load("Orders.Infrastructure");

    internal static Assembly OrdersHost { get; } = Load("Orders.Host");

    internal static Assembly LicensingDomain { get; } = Load("Teck.Cloud.Commerce.Licensing.Domain");

    internal static Assembly LicensingApplication { get; } = Load("Teck.Cloud.Commerce.Licensing.Application");

    internal static Assembly LicensingInfrastructure { get; } = Load("Teck.Cloud.Commerce.Licensing.Infrastructure");

    internal static Assembly LicensingHost { get; } = Load("Teck.Cloud.Commerce.Licensing.Host");

    internal static Assembly CustomerGatewayHost { get; } = Load("CustomerGateway.Host");

    internal static Assembly AdminGatewayHost { get; } = Load("AdminGateway.Host");

    internal static Assembly Contracts { get; } = typeof(Teck.Cloud.Commerce.Contracts.LicenseDesiredEvent).Assembly;

    internal static Assembly[] OrdersLayers { get; } =
        [OrdersDomain, OrdersApplication, OrdersInfrastructure, OrdersHost];

    internal static Assembly[] LicensingLayers { get; } =
        [LicensingDomain, LicensingApplication, LicensingInfrastructure, LicensingHost];

    private static Assembly Load(string assemblyName) => Assembly.Load(new AssemblyName(assemblyName));
}
