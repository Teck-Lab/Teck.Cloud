using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using Assembly = System.Reflection.Assembly;

namespace Web.Public.Gateway.Arch.UnitTests
{
    public abstract class BaseTest
    {
        protected static readonly Assembly PresentationAssembly = Assembly.Load("Web.Public.Gateway");
    }

    public abstract class ArchUnitBaseTest : BaseTest
    {
        private const string PresentationNamespacePattern = @"^Web.Public.Gateway(?:\..*)?$";

        protected static readonly Architecture Architecture = new ArchLoader().LoadAssemblies(
            PresentationAssembly
        ).Build();

        protected static readonly IObjectProvider<IType> PresentationLayer =
            ArchRuleDefinition.Types().That().ResideInNamespaceMatching(PresentationNamespacePattern).As("Gateway Layer");
    }
}
