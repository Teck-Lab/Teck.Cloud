namespace Web.Admin.Gateway.Arch.UnitTests.Layers
{
    public class LayerTests : ArchUnitBaseTest
    {
        [Fact]
        public void GatewayLayer_ShouldContain_GatewayTypes()
        {
            IReadOnlyList<Type> gatewayTypes = PresentationAssembly
                .GetTypes()
                .Where(type => type.Namespace is { } ns && ns.StartsWith("Web.Admin.Gateway", StringComparison.Ordinal))
                .ToList();

            Assert.NotEmpty(gatewayTypes);
        }
    }
}
