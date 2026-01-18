using System.Diagnostics.CodeAnalysis;

namespace Catalog.IntegrationTests.Shared
{
    [CollectionDefinition("SharedTestcontainers")]
    [SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "xUnit requires collection definition classes to be public")]
    public class SharedTestcontainersCollection : ICollectionFixture<SharedTestcontainersFixture> { }
}