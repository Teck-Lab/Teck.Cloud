using Teck.Cloud.Arch.Tests.Rules;

namespace Catalog.Arch.UnitTests.Domain;

public class AggregateRootTests : ArchUnitBaseTest
{
    [Fact]
    public void AggregateRoots_Should_FollowRules()
    {
        AggregateRootRules.AggregatesShouldInheritFromBaseEntity(Architecture);
        AggregateRootRules.AggregatesShouldResideInNamespace(Architecture, "Catalog.Domain");
        AggregateRootRules.AggregatesShouldOnlyExistInDomain(Architecture, "Catalog.Domain");
    }
}
