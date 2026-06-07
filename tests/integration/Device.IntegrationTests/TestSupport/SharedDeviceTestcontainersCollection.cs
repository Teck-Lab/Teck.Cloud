using Teck.Cloud.IntegrationTests.Shared;

namespace Device.IntegrationTests.TestSupport;

[CollectionDefinition("SharedTestcontainers")]
public sealed class SharedDeviceTestcontainersCollection : ICollectionFixture<SharedTestcontainersFixture>
{
}
