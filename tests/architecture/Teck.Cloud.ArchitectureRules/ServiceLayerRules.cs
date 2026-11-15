using ArchUnitNET.Domain;
using ArchUnitNET.xUnitV3;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Teck.Cloud.ArchitectureRules;

/// <summary>Enforces dependency direction between the four projects in a business service.</summary>
public static class ServiceLayerRules
{
    /// <summary>
    /// Ensures Domain depends on none of the other service layers, Application depends on neither
    /// Infrastructure nor Host, and Infrastructure does not depend on Host. Host is the composition
    /// boundary and may depend on all three lower layers.
    /// </summary>
    /// <param name="architecture">The architecture containing all four compiled service projects.</param>
    /// <param name="domainAssembly">The service Domain assembly.</param>
    /// <param name="applicationAssembly">The service Application assembly.</param>
    /// <param name="infrastructureAssembly">The service Infrastructure assembly.</param>
    /// <param name="hostAssembly">The service Host assembly.</param>
    public static void DependenciesShouldPointTowardHost(
        Architecture architecture,
        Assembly domainAssembly,
        Assembly applicationAssembly,
        Assembly infrastructureAssembly,
        Assembly hostAssembly)
    {
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(domainAssembly);
        ArgumentNullException.ThrowIfNull(applicationAssembly);
        ArgumentNullException.ThrowIfNull(infrastructureAssembly);
        ArgumentNullException.ThrowIfNull(hostAssembly);

        var layers = new[] { domainAssembly, applicationAssembly, infrastructureAssembly, hostAssembly };
        var duplicateAssemblies = layers
            .GroupBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
            .Where(group => group.Skip(1).Any())
            .Select(group => group.Key);
        Assert.True(!duplicateAssemblies.Any(), "Each service layer must be a distinct assembly: " + string.Join(", ", duplicateAssemblies));

        foreach (var assembly in layers)
        {
            Assert.True(assembly.GetTypes().Length > 0, $"Service layer assembly '{assembly.GetName().Name}' must contain compiled types.");
        }

        Types().That().ResideInAssembly(domainAssembly).Should()
            .NotDependOnAny(Types().That().ResideInAssembly(applicationAssembly)
                .Or().ResideInAssembly(infrastructureAssembly)
                .Or().ResideInAssembly(hostAssembly))
            .Because("Domain owns business rules and must not depend on outer service layers")
            .Check(architecture);

        Types().That().ResideInAssembly(applicationAssembly).Should()
            .NotDependOnAny(Types().That().ResideInAssembly(infrastructureAssembly)
                .Or().ResideInAssembly(hostAssembly))
            .Because("Application defines use cases over Domain and must not depend on adapters or transport")
            .Check(architecture);

        Types().That().ResideInAssembly(infrastructureAssembly).Should()
            .NotDependOnAny(Types().That().ResideInAssembly(hostAssembly))
            .Because("Infrastructure implements Application contracts and must not depend on the process entry point")
            .Check(architecture);
    }
}
