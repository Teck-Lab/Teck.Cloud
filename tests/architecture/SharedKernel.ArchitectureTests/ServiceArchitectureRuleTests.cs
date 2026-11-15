using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using SharedKernel.Core.Domain;
using Teck.Cloud.ArchitectureRules;
using Xunit;
using InvalidServiceDomain = ArchitectureRuleFixtures.Invalid.Domain.Entities;
using ValidServiceApplication = ArchitectureRuleFixtures.Valid.Application.Sales.Features.CreateOrder.V1;
using ValidServiceDomain = ArchitectureRuleFixtures.Valid.Domain.Aggregates.Orders;

namespace SharedKernel.ArchitectureTests;

/// <summary>Exercises the reusable business-service architecture rules on compiled fixtures.</summary>
public sealed class ServiceArchitectureRuleTests
{
    private const string DomainNamespace = "ArchitectureRuleFixtures.Valid.Domain";
    private const string ApplicationNamespace = "ArchitectureRuleFixtures.Valid.Application";

    /// <summary>A four-project service following the selected layer, aggregate and slice conventions passes.</summary>
    [Fact]
    public void ValidFourProjectServicePassesSharedRules()
    {
        var domainAssembly = typeof(ValidServiceDomain.Order).Assembly;
        var applicationAssembly = typeof(ValidServiceApplication.CreateOrderCommand).Assembly;
        var infrastructureAssembly = typeof(ArchitectureRuleFixtures.Valid.Infrastructure.Database.OrderStore).Assembly;
        var hostAssembly = typeof(ArchitectureRuleFixtures.Valid.Host.ServiceComposition).Assembly;
        var architecture = LoadArchitecture(domainAssembly, applicationAssembly, infrastructureAssembly, hostAssembly);

        SharedArchitectureRules.AssertAll(
            architecture,
            domainAssembly,
            applicationAssembly,
            infrastructureAssembly,
            hostAssembly,
            DomainNamespace,
            ApplicationNamespace);
    }

    /// <summary>A compiled Domain-to-Application reference violates the inward dependency boundary.</summary>
    [Fact]
    public void DomainReferenceToApplicationIsRejected()
    {
        var domainAssembly = typeof(InvalidServiceDomain.Order).Assembly;
        var applicationAssembly = typeof(ValidServiceApplication.CreateOrderCommand).Assembly;
        var infrastructureAssembly = typeof(ArchitectureRuleFixtures.Valid.Infrastructure.Database.OrderStore).Assembly;
        var hostAssembly = typeof(ArchitectureRuleFixtures.Valid.Host.ServiceComposition).Assembly;
        var architecture = LoadArchitecture(
            domainAssembly,
            applicationAssembly,
            infrastructureAssembly,
            hostAssembly,
            typeof(ValidServiceDomain.Order).Assembly);

        var failure = Assert.ThrowsAny<Exception>(() => ServiceLayerRules.DependenciesShouldPointTowardHost(
            architecture,
            domainAssembly,
            applicationAssembly,
            infrastructureAssembly,
            hostAssembly));

        Assert.Contains(nameof(InvalidServiceDomain.Order), failure.Message, StringComparison.Ordinal);
    }

    /// <summary>An Application-to-Infrastructure dependency violates the inward application boundary.</summary>
    [Fact]
    public void ApplicationReferenceToInfrastructureIsRejected()
    {
        var domainAssembly = typeof(ValidServiceDomain.Order).Assembly;
        var applicationAssembly = typeof(InvalidServiceDomain.Order).Assembly;
        var infrastructureAssembly = typeof(ArchitectureRuleFixtures.Valid.Infrastructure.Database.OrderStore).Assembly;
        var hostAssembly = typeof(ArchitectureRuleFixtures.Valid.Host.ServiceComposition).Assembly;
        var architecture = LoadArchitecture(domainAssembly, applicationAssembly, infrastructureAssembly, hostAssembly);

        var failure = Assert.ThrowsAny<Exception>(() => ServiceLayerRules.DependenciesShouldPointTowardHost(
            architecture,
            domainAssembly,
            applicationAssembly,
            infrastructureAssembly,
            hostAssembly));

        Assert.Contains(nameof(InvalidServiceDomain.Order), failure.Message, StringComparison.Ordinal);
    }

    /// <summary>An Infrastructure-to-Host dependency violates the adapter boundary.</summary>
    [Fact]
    public void InfrastructureReferenceToHostIsRejected()
    {
        var domainAssembly = typeof(ValidServiceDomain.Order).Assembly;
        var applicationAssembly = typeof(ValidServiceApplication.CreateOrderCommand).Assembly;
        var infrastructureAssembly = typeof(InvalidServiceDomain.Order).Assembly;
        var hostAssembly = typeof(ArchitectureRuleFixtures.Valid.Host.ServiceComposition).Assembly;
        var architecture = LoadArchitecture(domainAssembly, applicationAssembly, infrastructureAssembly, hostAssembly);

        var failure = Assert.ThrowsAny<Exception>(() => ServiceLayerRules.DependenciesShouldPointTowardHost(
            architecture,
            domainAssembly,
            applicationAssembly,
            infrastructureAssembly,
            hostAssembly));

        Assert.Contains(nameof(InvalidServiceDomain.Order), failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A technical Domain.Entities bucket fails the aggregate-first namespace convention.</summary>
    [Fact]
    public void AggregateRootsInTechnicalNamespaceAreRejected()
    {
        var failure = Assert.ThrowsAny<Exception>(() => AggregateNamespaceRules.TypesShouldBeGroupedByAggregate(
            typeof(InvalidServiceDomain.Order).Assembly,
            "ArchitectureRuleFixtures.Invalid.Domain"));

        Assert.Contains(nameof(InvalidServiceDomain.Order), failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A command/query outside a capability feature namespace fails the slice convention.</summary>
    [Fact]
    public void QueryOutsideVersionedFeatureSliceIsRejected()
    {
        var invalidApplication = typeof(ArchitectureRuleFixtures.Invalid.Application.Sales.Queries.GetOrderQuery).Assembly;
        var failure = Assert.ThrowsAny<Exception>(() => ApplicationSliceRules.TypesShouldFollowVerticalSlices(
            invalidApplication,
            "ArchitectureRuleFixtures.Invalid.Application"));

        Assert.Contains("GetOrderQuery", failure.Message, StringComparison.Ordinal);
        Assert.Contains("GetOrderHandler", failure.Message, StringComparison.Ordinal);
    }

    private static Architecture LoadArchitecture(params System.Reflection.Assembly[] serviceAssemblies) => new ArchLoader()
        .LoadAssemblies([.. serviceAssemblies, typeof(BaseEntity).Assembly])
        .Build();
}
