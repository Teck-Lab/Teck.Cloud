using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using SharedKernel.Core.Domain;
using Teck.Cloud.ArchitectureRules;
using Xunit;

namespace Commerce.ArchitectureTests;

/// <summary>Applies the shared four-project business-service rules to the commerce services.</summary>
public sealed class CommerceServiceArchitectureTests
{
    private static readonly Architecture OrdersArchitecture = new ArchLoader()
        .LoadAssemblies([.. CommerceAssemblies.OrdersLayers, typeof(BaseEntity).Assembly])
        .Build();

    private static readonly Architecture LicensingArchitecture = new ArchLoader()
        .LoadAssemblies([.. CommerceAssemblies.LicensingLayers, typeof(BaseEntity).Assembly])
        .Build();

    /// <summary>Orders keeps the Domain/Application/Infrastructure/Host direction and slice conventions.</summary>
    [Fact]
    public void OrdersFollowsSharedServiceArchitecture() =>
        SharedArchitectureRules.AssertAll(
            OrdersArchitecture,
            CommerceAssemblies.OrdersDomain,
            CommerceAssemblies.OrdersApplication,
            CommerceAssemblies.OrdersInfrastructure,
            CommerceAssemblies.OrdersHost,
            CommerceAssemblies.OrdersNamespace + ".Domain",
            CommerceAssemblies.OrdersNamespace + ".Application");

    /// <summary>Licensing keeps the Domain/Application/Infrastructure/Host direction and slice conventions.</summary>
    [Fact]
    public void LicensingFollowsSharedServiceArchitecture() =>
        SharedArchitectureRules.AssertAll(
            LicensingArchitecture,
            CommerceAssemblies.LicensingDomain,
            CommerceAssemblies.LicensingApplication,
            CommerceAssemblies.LicensingInfrastructure,
            CommerceAssemblies.LicensingHost,
            CommerceAssemblies.LicensingNamespace + ".Domain",
            CommerceAssemblies.LicensingNamespace + ".Application");
}
