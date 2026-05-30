using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using Assembly = System.Reflection.Assembly;

namespace Statistic.Arch.UnitTests
{
    public abstract class BaseTest
    {
        protected static readonly Assembly DomainAssembly = Assembly.Load("Statistic.Domain");
        protected static readonly Assembly ApplicationAssembly = Assembly.Load("Statistic.Application");
        protected static readonly Assembly InfrastructureAssembly = Assembly.Load("Statistic.Infrastructure");
        protected static readonly Assembly PresentationAssembly = Assembly.Load("Statistic.Api");
    }

    public abstract class ArchUnitBaseTest : BaseTest
    {
        private const string DomainNamespacePattern = @"^Statistic\.Domain(?:\..*)?$";
        private const string ApplicationNamespacePattern = @"^Statistic\.Application(?:\..*)?$";
        private const string InfrastructureNamespacePattern = @"^Statistic\.Infrastructure(?:\..*)?$";
        private const string PresentationNamespacePattern = @"^Statistic\.Api(?:\..*)?$";

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
