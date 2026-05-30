using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using Assembly = System.Reflection.Assembly;

namespace Image.Generator.Arch.UnitTests
{
    public abstract class BaseTest
    {
        protected static readonly Assembly ApplicationAssembly = Assembly.Load("Image.Generator.Application");
        protected static readonly Assembly PresentationAssembly = Assembly.Load("Image.Generator.Api");
    }

    public abstract class ArchUnitBaseTest : BaseTest
    {
        private const string ApplicationNamespacePattern = @"^Image\.Generator\.Application(?:\..*)?$";
        private const string PresentationNamespacePattern = @"^Image\.Generator\.Api(?:\..*)?$";

        protected static readonly Architecture Architecture = new ArchLoader().LoadAssemblies(
            ApplicationAssembly,
            PresentationAssembly
        ).Build();

        protected static readonly IObjectProvider<IType> ApplicationLayer =
            ArchRuleDefinition.Types().That().ResideInNamespaceMatching(ApplicationNamespacePattern).As("Application Layer");
        protected static readonly IObjectProvider<IType> PresentationLayer =
            ArchRuleDefinition.Types().That().ResideInNamespaceMatching(PresentationNamespacePattern).As("Presentation Layer");
    }
}
