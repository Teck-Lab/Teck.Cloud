using System.Reflection;
using Xunit;

namespace Commerce.ArchitectureTests;

/// <summary>Enforces the ownership boundary between commerce services, gateways and shared contracts.</summary>
public sealed class CommerceBoundaryTests
{
    /// <summary>Orders must consume Licensing only through versioned contracts, never its implementation.</summary>
    [Fact]
    public void OrdersDoNotReferenceLicensingImplementation() =>
        AssertDoesNotReference(
            CommerceAssemblies.OrdersLayers,
            CommerceAssemblies.LicensingLayers,
            "Orders must not reference Licensing implementation projects");

    /// <summary>Licensing must consume Orders only through versioned contracts, never its implementation.</summary>
    [Fact]
    public void LicensingDoNotReferenceOrdersImplementation() =>
        AssertDoesNotReference(
            CommerceAssemblies.LicensingLayers,
            CommerceAssemblies.OrdersLayers,
            "Licensing must not reference Orders implementation projects");

    /// <summary>Gateways are routing and authorization edges; they must not call service implementation types.</summary>
    [Fact]
    public void GatewaysDoNotReferenceServiceImplementations() =>
        AssertDoesNotReference(
            [CommerceAssemblies.CustomerGatewayHost, CommerceAssemblies.AdminGatewayHost],
            [.. CommerceAssemblies.OrdersLayers, .. CommerceAssemblies.LicensingLayers],
            "Gateways must not reference service implementation projects");

    /// <summary>Services must not depend on the gateway edge, which would invert the composition direction.</summary>
    [Fact]
    public void ServiceImplementationsDoNotReferenceGateways() =>
        AssertDoesNotReference(
            [.. CommerceAssemblies.OrdersLayers, .. CommerceAssemblies.LicensingLayers],
            [CommerceAssemblies.CustomerGatewayHost, CommerceAssemblies.AdminGatewayHost],
            "Services must not reference gateway projects");

    /// <summary>Shared commerce contracts stay free of service implementation dependencies.</summary>
    [Fact]
    public void SharedContractsDoNotReferenceCommerceImplementations() =>
        AssertDoesNotReference(
            [CommerceAssemblies.Contracts],
            [.. CommerceAssemblies.OrdersLayers, .. CommerceAssemblies.LicensingLayers],
            "Commerce.Contracts must not reference service implementation projects");

    /// <summary>
    /// Gateways only take direct references for routing, authorization and hosting. Persistence
    /// packages may flow transitively through SharedKernel.Infrastructure, so only direct metadata
    /// references are inspected here; runtime credentials are verified by the integration lane.
    /// </summary>
    [Fact]
    public void GatewaysDoNotTakeDirectPersistenceDependencies() =>
        AssertDoesNotReference(
            [CommerceAssemblies.CustomerGatewayHost, CommerceAssemblies.AdminGatewayHost],
            [
                "Microsoft.EntityFrameworkCore",
                "Microsoft.EntityFrameworkCore.Abstractions",
                "Microsoft.EntityFrameworkCore.Relational",
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                "SharedKernel.Persistence",
                "WolverineFx.EntityFrameworkCore",
            ],
            "Gateways must not take direct persistence dependencies");

    private static void AssertDoesNotReference(Assembly[] subjects, Assembly[] prohibited, string because) =>
        AssertDoesNotReference(
            subjects,
            prohibited.Select(assembly => assembly.GetName().Name!).ToArray(),
            because);

    private static void AssertDoesNotReference(Assembly[] subjects, string[] prohibitedNames, string because)
    {
        var violations = AssemblyBoundaryRules.FindProhibitedReferences(subjects, prohibitedNames);

        Assert.True(violations.Length == 0, $"{because}: {string.Join(", ", violations)}");
    }
}
