using Commerce.IntegrationTests.Infrastructure;
using Xunit;

namespace Commerce.IntegrationTests.Tests;

/// <summary>Regenerates the committed Wolverine handler sources for both commerce hosts from one disposable candidate copy.</summary>
/// <param name="keycloak">The shared disposable identity provider.</param>
[Collection(CommerceIntegrationSuite.Name)]
public sealed class CommerceHandlerGenerationTests(CommerceKeycloakFixture keycloak)
{
    /// <summary>Regenerates both hosts through the locked opt-in CLI while proving the ordinary Release build survived in the same tree.</summary>
    /// <returns>The generation assertion task.</returns>
    [Fact]
    public async Task GenerateCommerceHandlers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = CommerceHandlerGeneration.ResolveRoot();
        await using var stack = await CommerceStack.StartDependenciesAsync(keycloak, cancellationToken).ConfigureAwait(true);
        await new CommerceHandlerGeneration(root, stack).RunAsync(cancellationToken).ConfigureAwait(true);
    }
}
