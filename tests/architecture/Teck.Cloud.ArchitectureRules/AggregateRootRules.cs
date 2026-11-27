using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using SharedKernel.Core.Domain;
using Assembly = System.Reflection.Assembly;

namespace Teck.Cloud.ArchitectureRules;

/// <summary>Aggregate root inheritance and placement rules.</summary>
public static class AggregateRootRules
{
    /// <summary>Checks aggregates should inherit from base entity.</summary>
    /// <param name="architecture">architecture for AggregatesShouldInheritFromBaseEntity.</param>
    public static void AggregatesShouldInheritFromBaseEntity(Architecture architecture)
    {
        var rule = ArchRuleDefinition
            .Classes()
            .That().ImplementInterface(typeof(IAggregateRoot))
            .Should().BeAssignableTo(typeof(BaseEntity))
            .Because("all aggregate roots must inherit from BaseEntity");

        rule.Check(architecture);
    }

    /// <summary>Checks aggregate roots in one service Domain assembly inherit from BaseEntity.</summary>
    /// <param name="architecture">Loaded service architecture.</param>
    /// <param name="domainAssembly">The concrete service Domain assembly.</param>
    public static void AggregatesShouldInheritFromBaseEntity(Architecture architecture, Assembly domainAssembly)
    {
        var rule = ArchRuleDefinition
            .Classes()
            .That()
            .ResideInAssembly(domainAssembly)
            .And()
            .ImplementInterface(typeof(IAggregateRoot))
            .Should()
            .BeAssignableTo(typeof(BaseEntity<>))
            .Because("service aggregate roots must inherit from a shared BaseEntity<TId> type");

        rule.Check(architecture);
    }

    /// <summary>Checks aggregates should reside in namespace.</summary>
    /// <param name="architecture">architecture for AggregatesShouldResideInNamespace.</param>
    /// <param name="expectedNamespace">expectedNamespace for AggregatesShouldResideInNamespace.</param>
    public static void AggregatesShouldResideInNamespace(Architecture architecture, string expectedNamespace)
    {
        var rule = ArchRuleDefinition
            .Classes()
            .That().ImplementInterface(typeof(IAggregateRoot))
            .Should().ResideInNamespaceMatching(expectedNamespace)
            .Because("aggregate roots should live in the domain layer");

        rule.Check(architecture);
    }

    /// <summary>Checks aggregates should only exist in domain.</summary>
    /// <param name="architecture">architecture for AggregatesShouldOnlyExistInDomain.</param>
    /// <param name="domainNamespace">domainNamespace for AggregatesShouldOnlyExistInDomain.</param>
    public static void AggregatesShouldOnlyExistInDomain(Architecture architecture, string domainNamespace)
    {
        var rule = ArchRuleDefinition
            .Classes()
            .That()
            .ImplementInterface(typeof(IAggregateRoot))
            .Should()
            .ResideInNamespaceMatching(domainNamespace)
            .Because("aggregate roots should only exist in domain layer");

        rule.Check(architecture);
    }
}
