using Teck.Cloud.IntegrationTests.Shared;

namespace Location.IntegrationTests.TestSupport;

/// <summary>
/// xUnit collection definition that shares one <see cref="SharedTestcontainersFixture"/>
/// across all Location integration tests.
/// </summary>
[CollectionDefinition("LocationIntegrationTests", DisableParallelization = true)]
public sealed class LocationIntegrationTestsCollection : ICollectionFixture<SharedTestcontainersFixture>;
