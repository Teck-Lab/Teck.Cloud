using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Teck.Cloud.Arch.Tests.Rules;

namespace Catalog.Arch.UnitTests.Domain
{
    public class DomainTests : ArchUnitBaseTest
    {
        [Fact]
        public void DomainEvents_Should_FollowRules()
        {
            DomainEventRules.DomainEventsShouldBeSealed(Architecture);
            DomainEventRules.DomainEventsShouldHaveCorrectName(Architecture);
        }

        [Fact]
        public void Entities_Should_FollowRules()
        {
            DomainRules.EntitiesShouldInheritBaseEntity(Architecture);
            DomainRules.EntitiesShouldHavePrivateSetters(Architecture);
            DomainRules.EntityCreateMethodsShouldBeStatic(Architecture);
        }

        [Fact]
        public void ValueObjects_Should_BeImmutable()
        {
            var rule = ArchRuleDefinition
                .Classes()
                .That()
                .ResideInNamespace("Catalog.Domain.ValueObjects")
                .Should()
                .BeImmutable()
                .Because("value objects must be immutable")
                .WithoutRequiringPositiveResults();

            rule.Check(Architecture);
        }
    }
}
