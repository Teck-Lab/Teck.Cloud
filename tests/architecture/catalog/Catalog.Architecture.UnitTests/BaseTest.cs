using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using Catalog.Api;
using Catalog.Application;
using Catalog.Domain.Entities.BrandAggregate;
using Catalog.Infrastructure.Persistence;
using Assembly = System.Reflection.Assembly;

namespace Catalog.Arch.UnitTests
{
    public abstract class BaseTest
    {
        protected static readonly Assembly DomainAssembly = typeof(Brand).Assembly;
        protected static readonly Assembly ApplicationAssembly = typeof(ICatalogApplication).Assembly;
        protected static readonly Assembly InfrastructureAssembly = typeof(ApplicationWriteDbContext).Assembly;
        protected static readonly Assembly PresentationAssembly = typeof(IAssemblyMarker).Assembly;
    }

    public abstract class ArchUnitBaseTest : BaseTest
    {
        private const string DomainNamespacePattern = @"^Catalog\.Domain(?:\..*)?$";
        private const string ApplicationNamespacePattern = @"^Catalog\.Application(?:\..*)?$";
        private const string InfrastructureNamespacePattern = @"^Catalog\.Infrastructure(?:\..*)?$";
        private const string PresentationNamespacePattern = @"^Catalog\.Api(?:\..*)?$";

        protected static readonly Architecture Architecture = new ArchLoader().LoadAssemblies(
            DomainAssembly,
            ApplicationAssembly,
            InfrastructureAssembly,
            PresentationAssembly
        ).Build();

        protected static readonly IObjectProvider<IType> DomainLayer =
            ArchRuleDefinition.Types().That().ResideInNamespaceMatching(DomainNamespacePattern).As("Domain Layer");
        protected static readonly IObjectProvider<IType> ApplicationLayer =
            ArchRuleDefinition.Types().That().ResideInNamespaceMatching(ApplicationNamespacePattern).As("Application Layer");
        protected static readonly IObjectProvider<IType> InfrastructureLayer =
            ArchRuleDefinition.Types().That().ResideInNamespaceMatching(InfrastructureNamespacePattern).As("Infrastructure Layer");
        protected static readonly IObjectProvider<IType> PresentationLayer =
            ArchRuleDefinition.Types().That().ResideInNamespaceMatching(PresentationNamespacePattern).As("Presentation Layer");
    }
}
