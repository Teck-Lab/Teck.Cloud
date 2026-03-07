using Catalog.Api.Endpoints.V1.Service;
using Catalog.Api.Grpc.V1;
using SharedKernel.Grpc.Contracts.Remote.V1.ServiceVersions;
using Shouldly;

namespace Catalog.UnitTests.Api;

public class ServiceVersionTests
{
    [Fact]
    public void ResolveVersion_ShouldReturnNonEmptyValue()
    {
        // Act
        var version = CatalogVersionResolver.ResolveVersion();

        // Assert
        version.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnCatalogServiceVersion()
    {
        // Arrange
        GetCatalogServiceVersionCommandHandler handler = new();
        GetCatalogServiceVersionCommand command = new();

        // Act
        ServiceVersionRpcResult result = await handler.ExecuteAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.Service.ShouldBe("catalog");
        result.Version.ShouldNotBeNullOrWhiteSpace();
    }
}
