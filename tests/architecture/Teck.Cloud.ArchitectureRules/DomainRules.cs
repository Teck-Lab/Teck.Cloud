using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using SharedKernel.Core.Domain;
using Assembly = System.Reflection.Assembly;

namespace Teck.Cloud.ArchitectureRules;

/// <summary>Domain entity and aggregate placement rules.</summary>
public static class DomainRules
{
    /// <summary>Checks entities should inherit base entity.</summary>
    /// <param name="architecture">architecture for EntitiesShouldInheritBaseEntity.</param>
    public static void EntitiesShouldInheritBaseEntity(Architecture architecture)
    {
        var rule = ArchRuleDefinition.Classes()
            .That()
            .ImplementInterface(typeof(IBaseEntity))
            .Should()
            .BeAssignableTo(typeof(BaseEntity))
            .Because("entities should inherit from BaseEntity");

        rule.Check(architecture);
    }

    /// <summary>Checks entities in one service Domain assembly inherit the shared entity base.</summary>
    /// <param name="architecture">Loaded service architecture.</param>
    /// <param name="domainAssembly">The concrete service Domain assembly.</param>
    public static void EntitiesShouldInheritBaseEntity(Architecture architecture, Assembly domainAssembly)
    {
        var rule = ArchRuleDefinition.Classes()
            .That()
            .ResideInAssembly(domainAssembly)
            .And()
            .ImplementInterface(typeof(IBaseEntity))
            .Should()
            .BeAssignableTo(typeof(BaseEntity<>))
            .Because("service entities should inherit from a shared BaseEntity<TId> type");

        rule.Check(architecture);
    }

    // A previous "AggregatesShouldTrackDomainEvents" rule was removed here. It asserted that
    // each aggregate declares a `_domainEvents` field and `AddDomainEvent`/`ClearDomainEvents`/
    // `GetDomainEvents` members. Those members are provided by the `BaseEntity<TId>` base class (and
    // the getter is a `DomainEvents` property, not a `GetDomainEvents` method), so the rule both
    // checked for a member that does not exist and duplicated the inherit-from-BaseEntity guarantee
    // already enforced by EntitiesShouldInheritBaseEntity / AggregatesShouldInheritFromBaseEntity.

    /// <summary>Checks entities should have private setters.</summary>
    /// <param name="architecture">architecture for EntitiesShouldHavePrivateSetters.</param>
    public static void EntitiesShouldHavePrivateSetters(Architecture architecture)
    {
        var rule = ArchRuleDefinition.Members()
            .That()
            .HaveNameStartingWith("set_")
            .And()
            .AreDeclaredIn(ArchRuleDefinition.Classes().That().ImplementInterface(typeof(IBaseEntity)))

            // TenantId is a public-settable property mandated by the ITenantScoped contract (the EF
            // SaveChanges tenant interceptor assigns it), so it is exempt from the private-setter rule.
            // (Member names carry their parameter signature, e.g. "set_TenantId(System.String)", so
            // match by substring rather than exact name.)
            .And()
            .DoNotHaveNameContaining("set_TenantId")
            .Should()
            .NotBePublic()
            .Because("entity properties should have private setters for encapsulation")
            .WithoutRequiringPositiveResults();

        rule.Check(architecture);
    }

    /// <summary>Checks entities in one service Domain assembly have private setters.</summary>
    /// <param name="architecture">Loaded service architecture.</param>
    /// <param name="domainAssembly">The concrete service Domain assembly.</param>
    public static void EntitiesShouldHavePrivateSetters(Architecture architecture, Assembly domainAssembly)
    {
        var entityClasses = ArchRuleDefinition.Classes()
            .That()
            .ResideInAssembly(domainAssembly)
            .And()
            .ImplementInterface(typeof(IBaseEntity));
        var rule = ArchRuleDefinition.Members()
            .That()
            .HaveNameStartingWith("set_")
            .And()
            .AreDeclaredIn(entityClasses)
            .And()
            .DoNotHaveNameContaining("set_TenantId")
            .Should()
            .NotBePublic()
            .Because("entity properties should have private setters for encapsulation")
            .WithoutRequiringPositiveResults();

        rule.Check(architecture);
    }

    /// <summary>Checks aggregate roots should be in domain layer.</summary>
    /// <param name="architecture">architecture for AggregateRootsShouldBeInDomainLayer.</param>
    public static void AggregateRootsShouldBeInDomainLayer(Architecture architecture)
    {
        var rule = ArchRuleDefinition.Classes()
            .That()
            .ImplementInterface(typeof(IAggregateRoot))
            .Should()
            .ResideInNamespaceMatching("Domain.Entities")
            .Because("aggregate roots should be in the domain layer");

        rule.Check(architecture);
    }

    /// <summary>Checks entity create methods should be static.</summary>
    /// <param name="architecture">architecture for EntityCreateMethodsShouldBeStatic.</param>
    public static void EntityCreateMethodsShouldBeStatic(Architecture architecture)
    {
        var rule = ArchRuleDefinition.Members()
            .That()
            .HaveNameStartingWith("Create")
            .And()
            .AreDeclaredIn(ArchRuleDefinition.Classes().That().ImplementInterface(typeof(IBaseEntity)))
            .Should()
            .BeStatic()
            .Because("entity factory methods should be static")
            .WithoutRequiringPositiveResults();

        rule.Check(architecture);
    }

    /// <summary>Checks factory methods on entities in one service Domain assembly are static.</summary>
    /// <param name="architecture">Loaded service architecture.</param>
    /// <param name="domainAssembly">The concrete service Domain assembly.</param>
    public static void EntityCreateMethodsShouldBeStatic(Architecture architecture, Assembly domainAssembly)
    {
        var entityClasses = ArchRuleDefinition.Classes()
            .That()
            .ResideInAssembly(domainAssembly)
            .And()
            .ImplementInterface(typeof(IBaseEntity));
        var rule = ArchRuleDefinition.Members()
            .That()
            .HaveNameStartingWith("Create")
            .And()
            .AreDeclaredIn(entityClasses)
            .Should()
            .BeStatic()
            .Because("entity factory methods should be static")
            .WithoutRequiringPositiveResults();

        rule.Check(architecture);
    }
}
