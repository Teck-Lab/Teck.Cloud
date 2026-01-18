using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;

namespace Catalog.Arch.UnitTests.Layers
{
    public class LayerTests : ArchUnitBaseTest
    {
        [Fact]
        public void DomainLayer_ShouldNot_HaveDependencyOn_ApplicationLayer()
        {
            ArchRuleDefinition
                .Types()
                .That()
                .Are(DomainLayer)
                .Should()
                .NotDependOnAny(ApplicationLayer)
                .Because("domain layer should not depend on application layer")
                .Check(Architecture);
        }

        [Fact]
        public void DomainLayer_ShouldNot_HaveDependencyOn_InfrastructureLayer()
        {
            ArchRuleDefinition
                .Types()
                .That()
                .Are(DomainLayer)
                .Should()
                .NotDependOnAny(InfrastructureLayer)
                .Because("domain layer should not depend on infrastructure layer")
                .Check(Architecture);
        }

        [Fact]
        public void ApplicationLayer_ShouldNot_HaveDependencyOn_PresentationLayer()
        {
            ArchRuleDefinition
                .Types()
                .That()
                .Are(ApplicationLayer)
                .Should()
                .NotDependOnAny(PresentationLayer)
                .Because("application layer should not depend on presentation layer")
                .Check(Architecture);
        }

        [Fact]
        public void ApplicationLayer_ShouldNot_HaveDependencyOn_InfrastructureLayer()
        {
            ArchRuleDefinition
                .Types()
                .That()
                .Are(ApplicationLayer)
                .Should()
                .NotDependOnAny(InfrastructureLayer)
                .Because("application layer should not depend on infrastructure layer")
                .Check(Architecture);
        }

        [Fact]
        public void InfrastructureLayer_ShouldNot_HaveDependencyOn_PresentationLayer()
        {
            ArchRuleDefinition
                .Types()
                .That()
                .Are(InfrastructureLayer)
                .Should()
                .NotDependOnAny(PresentationLayer)
                .Because("infrastructure layer should not depend on presentation layer")
                .Check(Architecture);
        }
    }
}
