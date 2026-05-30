using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;

namespace Image.Generator.Arch.UnitTests.Layers
{
    public class LayerTests : ArchUnitBaseTest
    {
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
    }
}
