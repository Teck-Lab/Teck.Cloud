using ArchUnitNET.Loader;
using SharedKernel.Core.Domain;
using Teck.Cloud.ArchitectureRules;
using Xunit;

namespace Orders.Architecture.UnitTests;

/// <summary>Applies the shared four-project business-service rules to the Orders service.</summary>
public sealed class OrdersServiceArchitectureTests
{
    private static readonly ArchUnitNET.Domain.Architecture OrdersArchitecture = new ArchLoader()
        .LoadAssemblies([.. OrdersAssemblies.Layers, typeof(BaseEntity).Assembly])
        .Build();

    /// <summary>Orders keeps the Domain/Application/Infrastructure/Host direction and slice conventions.</summary>
    [Fact]
    public void OrdersFollowsSharedServiceArchitecture() =>
        SharedArchitectureRules.AssertAll(
            OrdersArchitecture,
            OrdersAssemblies.Domain,
            OrdersAssemblies.Application,
            OrdersAssemblies.Infrastructure,
            OrdersAssemblies.Host,
            OrdersAssemblies.OrdersNamespace + ".Domain",
            OrdersAssemblies.OrdersNamespace + ".Application");
}
