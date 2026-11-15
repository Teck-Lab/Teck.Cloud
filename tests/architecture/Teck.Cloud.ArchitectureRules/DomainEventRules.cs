using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using SharedKernel.Core.Events;
using Assembly = System.Reflection.Assembly;

namespace Teck.Cloud.ArchitectureRules;

/// <summary>Domain event shape and placement rules.</summary>
public static class DomainEventRules
{
    /// <summary>Checks domain events should be sealed.</summary>
    /// <param name="architecture">architecture for DomainEventsShouldBeSealed.</param>
    public static void DomainEventsShouldBeSealed(Architecture architecture)
    {
        var rule = ArchRuleDefinition
            .Classes()
            .That()
            .ImplementInterface(typeof(IDomainEvent))
            .Should()
            .BeSealed()
            .Because("domain events should be sealed to prevent inheritance")
            .WithoutRequiringPositiveResults();

        rule.Check(architecture);
    }

    /// <summary>Checks domain events in one service Domain assembly are sealed.</summary>
    /// <param name="architecture">Loaded service architecture.</param>
    /// <param name="domainAssembly">The concrete service Domain assembly.</param>
    public static void DomainEventsShouldBeSealed(Architecture architecture, Assembly domainAssembly)
    {
        var rule = ArchRuleDefinition
            .Classes()
            .That()
            .ResideInAssembly(domainAssembly)
            .And()
            .ImplementInterface(typeof(IDomainEvent))
            .Should()
            .BeSealed()
            .Because("domain events should be sealed to prevent inheritance")
            .WithoutRequiringPositiveResults();

        rule.Check(architecture);
    }

    /// <summary>Checks domain events should reside in domain events namespace.</summary>
    /// <param name="architecture">architecture for DomainEventsShouldResideInDomainEventsNamespace.</param>
    public static void DomainEventsShouldResideInDomainEventsNamespace(Architecture architecture)
    {
        // The codebase names domain events by namespace (e.g. Orders.Domain.DomainEvents.OrderPlaced),
        // not by a "DomainEvent" suffix, so the convention enforced here is the namespace.
        var rule = ArchRuleDefinition
            .Classes()
            .That()
            .ImplementInterface(typeof(IDomainEvent))
            .Should()
            .ResideInNamespaceMatching("Domain.DomainEvents")
            .Because("domain events should live in the Domain.DomainEvents namespace")
            .WithoutRequiringPositiveResults();

        rule.Check(architecture);
    }
}
