using Xunit;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>One disposable Keycloak shared by every real commerce integration test.</summary>
[CollectionDefinition(Name)]
public sealed class CommerceIntegrationSuite : ICollectionFixture<CommerceKeycloakFixture>
{
    /// <summary>The collection name used by all commerce integration tests.</summary>
    public const string Name = "CommerceIntegration";
}
