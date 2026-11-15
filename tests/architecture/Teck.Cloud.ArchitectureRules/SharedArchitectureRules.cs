using ArchUnitNET.Domain;
using Assembly = System.Reflection.Assembly;

namespace Teck.Cloud.ArchitectureRules;

/// <summary>
/// Single entry point for a concrete four-project business service architecture suite.
/// Build the architecture from the service's Domain, Application, Infrastructure and Host
/// assemblies, then pass those same assemblies and their root namespaces to <see cref="AssertAll"/>.
/// </summary>
public static class SharedArchitectureRules
{
    /// <summary>Runs shared layer, namespace, CQRS, domain model and persistence-shape rules.</summary>
    /// <param name="architecture">The architecture loaded from all four service assemblies and shared contracts.</param>
    /// <param name="domainAssembly">The concrete service Domain assembly.</param>
    /// <param name="applicationAssembly">The concrete service Application assembly.</param>
    /// <param name="infrastructureAssembly">The concrete service Infrastructure assembly.</param>
    /// <param name="hostAssembly">The concrete service Host assembly.</param>
    /// <param name="domainNamespace">The Domain root namespace, for example <c lang="csharp">Orders.Domain</c>.</param>
    /// <param name="applicationNamespace">The Application root namespace, for example <c lang="csharp">Orders.Application</c>.</param>
    public static void AssertAll(
        Architecture architecture,
        Assembly domainAssembly,
        Assembly applicationAssembly,
        Assembly infrastructureAssembly,
        Assembly hostAssembly,
        string domainNamespace,
        string applicationNamespace)
    {
        ServiceLayerRules.DependenciesShouldPointTowardHost(
            architecture,
            domainAssembly,
            applicationAssembly,
            infrastructureAssembly,
            hostAssembly);
        AggregateNamespaceRules.TypesShouldBeGroupedByAggregate(domainAssembly, domainNamespace);
        ApplicationSliceRules.TypesShouldFollowVerticalSlices(applicationAssembly, applicationNamespace);
        MapperRules.AutoMapperReferencesShouldNotBeUsed(domainAssembly, applicationAssembly, infrastructureAssembly, hostAssembly);

        // CQRS handlers (Wolverine static Handle methods).
        CommandHandlerRules.CommandHandlersShouldBeStaticClassesEndingWithHandler(applicationAssembly);
        CommandHandlerRules.CommandHandlersShouldResideInFeaturesNamespace(applicationAssembly);
        CommandHandlerRules.CommandHandlersShouldNotUseReadRepositories(applicationAssembly);
        CommandHandlerRules.CommandHandlersShouldUseWriteRepositories(applicationAssembly);
        CommandHandlerRules.CommandsShouldBeImmutable(architecture);

        QueryHandlerRules.QueryHandlersShouldBeStaticClassesEndingWithHandler(applicationAssembly);
        QueryHandlerRules.QueryHandlersShouldResideInFeaturesNamespace(applicationAssembly);
        QueryHandlerRules.QueryHandlersShouldNotUseWriteRepositories(applicationAssembly);
        QueryHandlerRules.QueryHandlersShouldUseReadRepositoriesOrCaches(applicationAssembly);
        QueryHandlerRules.QueryHandlersShouldNotUseMutablePersistence(applicationAssembly);
        QueryHandlerRules.QueriesShouldNotModifyState(architecture);

        // Domain model. Aggregate placement is validated against the service's aggregate-first namespace.
        DomainRules.EntitiesShouldInheritBaseEntity(architecture, domainAssembly);
        DomainRules.EntitiesShouldHavePrivateSetters(architecture, domainAssembly);
        DomainRules.EntityCreateMethodsShouldBeStatic(architecture, domainAssembly);
        AggregateRootRules.AggregatesShouldInheritFromBaseEntity(architecture, domainAssembly);
        DomainEventRules.DomainEventsShouldBeSealed(architecture, domainAssembly);
        ValidationRules.ValidatorsShouldBeSealed(architecture, applicationAssembly);
        ReadModelRules.ReadModelsShouldHaveExplicitNames(infrastructureAssembly);
    }
}
