using Catalog.Application;
using Teck.Cloud.Arch.Tests.Rules;

namespace Catalog.Arch.UnitTests.Application
{
    public class ApplicationTests : ArchUnitBaseTest
    {
        [Fact]
        public void CommandHandlers_Should_FollowRules()
        {
            CommandHandlerRules.CommandHandlersShouldBeSealed(Architecture);
            CommandHandlerRules.CommandHandlersShouldResideInFeaturesNamespace(Architecture, "Catalog.Application");
            CommandHandlerRules.CommandHandlersShouldResideInApplicationAssembly(Architecture, typeof(ICatalogApplication).Assembly);
            CommandHandlerRules.CommandHandlersShouldHaveCorrectName(Architecture);
            CommandHandlerRules.CommandHandlersShouldNotBePublic(Architecture);
            CommandHandlerRules.CommandsShouldBeImmutable(Architecture);
        }

        [Fact]
        public void QueryHandlers_Should_FollowRules()
        {
            QueryHandlerRules.QueryHandlersShouldHaveCorrectName(Architecture);
            QueryHandlerRules.QueryHandlersShouldNotBePublic(Architecture);
        }
    }
}
