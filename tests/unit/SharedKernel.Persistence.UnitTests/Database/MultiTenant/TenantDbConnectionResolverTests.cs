using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using SharedKernel.Core.Pricing;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Persistence.Database.MultiTenant;
using ZiggyCreatures.Caching.Fusion;
#pragma warning disable CA2000

namespace SharedKernel.Persistence.UnitTests.Database.MultiTenant;

public sealed class TenantDbConnectionResolverTests
{
    [Fact]
    public void ResolveTenantConnection_UsesSharedDefaults_WhenGatewayHintIsShared()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-TenantId"] = "tenant-shared-1";
        httpContext.Request.Headers["X-Tenant-DbStrategy"] = "Shared";

        var serviceProvider = BuildServiceProvider(httpContext);
        var resolver = new TenantDbConnectionResolver(
            serviceProvider,
            "Host=shared-write;",
            "Host=shared-read;",
            DatabaseProvider.PostgreSQL);

        var tenant = new TenantDetails
        {
            Id = "tenant-shared-1",
            DatabaseStrategy = "Dedicated",
            WriteConnectionString = "Host=tenant-dedicated;",
            DatabaseProvider = "PostgreSQL",
        };

        // Act
        var result = resolver.ResolveTenantConnection(tenant);

        // Assert
        result.Strategy.ShouldBe(DatabaseStrategy.Shared);
        result.WriteConnectionString.ShouldBe("Host=shared-write;");
        result.ReadConnectionString.ShouldBe("Host=shared-read;");
    }

    [Fact]
    public void ResolveTenantConnection_FallsBack_WhenGatewayHintTenantDoesNotMatch()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-TenantId"] = "tenant-other";
        httpContext.Request.Headers["X-Tenant-DbStrategy"] = "Shared";

        var serviceProvider = BuildServiceProvider(httpContext, returnErrorFromCustomerApi: true);
        var resolver = new TenantDbConnectionResolver(
            serviceProvider,
            "Host=shared-write;",
            "Host=shared-read;",
            DatabaseProvider.PostgreSQL);

        var tenant = new TenantDetails
        {
            Id = "tenant-dedicated-1",
            DatabaseStrategy = "Dedicated",
            WriteConnectionString = "Host=tenant-dedicated;",
            ReadConnectionString = "Host=tenant-dedicated-read;",
            DatabaseProvider = "PostgreSQL",
            HasReadReplicas = true,
        };

        // Act
        var result = resolver.ResolveTenantConnection(tenant);

        // Assert
        result.Strategy.ShouldBe(DatabaseStrategy.Dedicated);
        result.WriteConnectionString.ShouldBe("Host=tenant-dedicated;");
        result.ReadConnectionString.ShouldBe("Host=tenant-dedicated-read;");
    }

    private static ServiceProvider BuildServiceProvider(HttpContext httpContext, bool returnErrorFromCustomerApi = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });
        services.AddSingleton<IFusionCache>(new FusionCache(new FusionCacheOptions()));

        var handler = returnErrorFromCustomerApi
            ? new StatusCodeHttpMessageHandler(System.Net.HttpStatusCode.InternalServerError)
            : new StatusCodeHttpMessageHandler(System.Net.HttpStatusCode.OK, "{\"strategy\":\"Shared\"}");

        var customerClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://customer.local/")
        };

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("CustomerApi").Returns(customerClient);
        services.AddSingleton(httpClientFactory);

        return services.BuildServiceProvider();
    }

    private sealed class StatusCodeHttpMessageHandler : HttpMessageHandler
    {
        private readonly System.Net.HttpStatusCode _statusCode;
        private readonly string _body;

        public StatusCodeHttpMessageHandler(System.Net.HttpStatusCode statusCode, string body = "{}")
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body)
            };

            return Task.FromResult(response);
        }
    }
}
#pragma warning restore CA2000
