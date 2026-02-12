using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Migration.Models;
using SharedKernel.Migration.Services;
using Shouldly;

namespace SharedKernel.Migration.UnitTests.Services;

public class CustomerApiClientTests
{
    [Fact]
    public async Task UpdateMigrationStatusAsync_ShouldReturnTrue_WhenSuccessful()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler
        {
            ResponseMessage = new HttpResponseMessage(HttpStatusCode.OK),
        };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("CustomerApi").Returns(httpClient);
        
        var logger = Substitute.For<ILogger<CustomerApiClient>>();
        var client = new CustomerApiClient(httpClientFactory, logger);
        
        var tenantId = Guid.NewGuid().ToString();
        var serviceName = "catalog";

        // Act
        var result = await client.UpdateMigrationStatusAsync(
            tenantId,
            serviceName,
            MigrationStatus.Completed,
            "v1.0.0",
            null,
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Method.ShouldBe(HttpMethod.Put);
        handler.LastRequest.RequestUri!.ToString().ShouldContain($"/tenants/{tenantId}/services/{serviceName}/migration-status");
    }

    [Fact]
    public async Task UpdateMigrationStatusAsync_ShouldReturnFalse_WhenHttpError()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler
        {
            ResponseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError),
        };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("CustomerApi").Returns(httpClient);
        
        var logger = Substitute.For<ILogger<CustomerApiClient>>();
        var client = new CustomerApiClient(httpClientFactory, logger);
        
        var tenantId = Guid.NewGuid().ToString();
        var serviceName = "catalog";

        // Act
        var result = await client.UpdateMigrationStatusAsync(
            tenantId,
            serviceName,
            MigrationStatus.Failed,
            null,
            "Migration error",
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateMigrationStatusAsync_ShouldReturnFalse_WhenExceptionThrown()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler
        {
            ShouldThrow = true,
        };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("CustomerApi").Returns(httpClient);
        
        var logger = Substitute.For<ILogger<CustomerApiClient>>();
        var client = new CustomerApiClient(httpClientFactory, logger);
        
        var tenantId = Guid.NewGuid().ToString();
        var serviceName = "catalog";

        // Act
        var result = await client.UpdateMigrationStatusAsync(
            tenantId,
            serviceName,
            MigrationStatus.InProgress,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GetServiceDatabaseInfoAsync_ShouldReturnInfo_WhenSuccessful()
    {
        // Arrange
        var expectedInfo = new ServiceDatabaseInfo
        {
            VaultWritePath = "database/tenants/123/catalog/write",
            VaultReadPath = "database/tenants/123/catalog/read",
            HasSeparateReadDatabase = true,
        };

        using var handler = new TestHttpMessageHandler
        {
            ResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedInfo),
            },
        };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("CustomerApi").Returns(httpClient);
        
        var logger = Substitute.For<ILogger<CustomerApiClient>>();
        var client = new CustomerApiClient(httpClientFactory, logger);
        
        var tenantId = Guid.NewGuid().ToString();
        var serviceName = "catalog";

        // Act
        var result = await client.GetServiceDatabaseInfoAsync(
            tenantId,
            serviceName,
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.VaultWritePath.ShouldBe("database/tenants/123/catalog/write");
        result.VaultReadPath.ShouldBe("database/tenants/123/catalog/read");
        result.HasSeparateReadDatabase.ShouldBeTrue();
    }

    [Fact]
    public async Task GetServiceDatabaseInfoAsync_ShouldReturnNull_WhenHttpError()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler
        {
            ResponseMessage = new HttpResponseMessage(HttpStatusCode.NotFound),
        };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("CustomerApi").Returns(httpClient);
        
        var logger = Substitute.For<ILogger<CustomerApiClient>>();
        var client = new CustomerApiClient(httpClientFactory, logger);
        
        var tenantId = Guid.NewGuid().ToString();
        var serviceName = "catalog";

        // Act
        var result = await client.GetServiceDatabaseInfoAsync(
            tenantId,
            serviceName,
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetServiceDatabaseInfoAsync_ShouldReturnNull_WhenExceptionThrown()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler
        {
            ShouldThrow = true,
        };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("CustomerApi").Returns(httpClient);
        
        var logger = Substitute.For<ILogger<CustomerApiClient>>();
        var client = new CustomerApiClient(httpClientFactory, logger);
        
        var tenantId = Guid.NewGuid().ToString();
        var serviceName = "catalog";

        // Act
        var result = await client.GetServiceDatabaseInfoAsync(
            tenantId,
            serviceName,
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    // Test HttpMessageHandler for mocking HTTP responses
    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        public HttpResponseMessage? ResponseMessage { get; set; }
        public bool ShouldThrow { get; set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;

            if (ShouldThrow)
            {
                throw new HttpRequestException("Test exception");
            }

            return Task.FromResult(ResponseMessage ?? new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
