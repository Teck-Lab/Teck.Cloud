// <copyright file="SharedDeviceTestcontainersCollection.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.IntegrationTests.TestSupport;

[CollectionDefinition("SharedDeviceTestcontainers")]
public sealed class SharedDeviceTestcontainersCollection : ICollectionFixture<SharedDeviceTestcontainersFixture>
{
}
