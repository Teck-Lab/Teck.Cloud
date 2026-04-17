using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Core.Pricing;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Persistence.Database.MultiTenant;
using Shouldly;
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
    public void ResolveTenantConnection_UsesSharedDefaults_WhenNoHeaderAndDefaultStrategyIsShared()
    {
        // Arrange â€” no headers at all, defaultStrategy = "Shared"
        var httpContext = new DefaultHttpContext();
        var serviceProvider = BuildServiceProvider(httpContext);
        var resolver = new TenantDbConnectionResolver(
            serviceProvider,
            "Host=shared-write;",
            "Host=shared-read;",
            DatabaseProvider.PostgreSQL);

        var tenant = new TenantDetails
        {
            Id = "tenant-1",
            DatabaseStrategy = "Dedicated",
            DatabaseProvider = "PostgreSQL",
        };

        // Act
        var result = resolver.ResolveTenantConnection(tenant);

        // Assert â€” falls back to default strategy (Shared)
        result.Strategy.ShouldBe(DatabaseStrategy.Shared);
        result.WriteConnectionString.ShouldBe("Host=shared-write;");
    }

    [Fact]
    public void ResolveTenantConnection_CallsVaultProvider_WhenGatewayHintIsDedicated()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-DbStrategy"] = "Dedicated";

        var serviceProvider = BuildServiceProvider(httpContext);

        var vaultProvider = Substitute.For<IVaultTenantConnectionProvider>();
        vaultProvider
            .TryGetCached("tenant-dedicated-1", out Arg.Any<(string Write, string? Read)>())
            .Returns(callInfo =>
            {
                callInfo[1] = ("Host=vault-write;", (string?)"Host=vault-read;");
                return true;
            });

        var resolver = new TenantDbConnectionResolver(
            serviceProvider,
            "Host=shared-write;",
            "Host=shared-read;",
            DatabaseProvider.PostgreSQL,
            vaultProvider);

        var tenant = new TenantDetails
        {
            Id = "tenant-dedicated-1",
            Identifier = "tenant-dedicated-1",
            DatabaseStrategy = "Dedicated",
            DatabaseProvider = "PostgreSQL",
            HasReadReplicas = true,
        };

        // Act
        var result = resolver.ResolveTenantConnection(tenant);

        // Assert
        result.Strategy.ShouldBe(DatabaseStrategy.Dedicated);
        result.WriteConnectionString.ShouldBe("Host=vault-write;");
        result.ReadConnectionString.ShouldBe("Host=vault-read;");
        result.Provider.ShouldBe(DatabaseProvider.PostgreSQL);
    }

    [Fact]
    public void ResolveTenantConnection_OmitsReadConnection_WhenNoReadReplicas()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-DbStrategy"] = "Dedicated";

        var serviceProvider = BuildServiceProvider(httpContext);

        var vaultProvider = Substitute.For<IVaultTenantConnectionProvider>();
        vaultProvider
            .TryGetCached("tenant-1", out Arg.Any<(string Write, string? Read)>())
            .Returns(callInfo =>
            {
                callInfo[1] = ("Host=vault-write;", (string?)"Host=vault-read;");
                return true;
            });

        var resolver = new TenantDbConnectionResolver(
            serviceProvider,
            "Host=shared-write;",
            "Host=shared-read;",
            DatabaseProvider.PostgreSQL,
            vaultProvider);

        var tenant = new TenantDetails
        {
            Id = "tenant-1",
            Identifier = "tenant-1",
            DatabaseStrategy = "Dedicated",
            DatabaseProvider = "PostgreSQL",
            HasReadReplicas = false,   // <--- no read replicas
        };

        // Act
        var result = resolver.ResolveTenantConnection(tenant);

        // Assert â€” read connection must be null when HasReadReplicas is false
        result.WriteConnectionString.ShouldBe("Host=vault-write;");
        result.ReadConnectionString.ShouldBeNull();
    }

    [Fact]
    public void ResolveTenantConnection_ThrowsTenantConnectionNotFoundException_WhenVaultNotConfiguredAndDedicated()
    {
        // Arrange â€” NullVaultTenantConnectionProvider (default when vaultProvider param is omitted)
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-DbStrategy"] = "Dedicated";

        var serviceProvider = BuildServiceProvider(httpContext);
        var resolver = new TenantDbConnectionResolver(
            serviceProvider,
            "Host=shared-write;",
            "Host=shared-read;",
            DatabaseProvider.PostgreSQL);   // no vaultProvider â†’ NullVaultTenantConnectionProvider

        var tenant = new TenantDetails
        {
            Id = "tenant-dedicated-1",
            Identifier = "tenant-dedicated-1",
            DatabaseStrategy = "Dedicated",
            DatabaseProvider = "PostgreSQL",
        };

        // Act & Assert
        Should.Throw<TenantConnectionNotFoundException>(() => resolver.ResolveTenantConnection(tenant));
    }

    [Fact]
    public void ResolveTenantConnection_ReturnsDefaults_WhenTenantInfoIsNull()
    {
        // Arrange
        var serviceProvider = BuildServiceProvider(new DefaultHttpContext());
        var resolver = new TenantDbConnectionResolver(
            serviceProvider,
            "Host=shared-write;",
            "Host=shared-read;",
            DatabaseProvider.PostgreSQL);

        // Act
        var result = resolver.ResolveTenantConnection(null!);

        // Assert
        result.WriteConnectionString.ShouldBe("Host=shared-write;");
        result.ReadConnectionString.ShouldBe("Host=shared-read;");
        result.Provider.ShouldBe(DatabaseProvider.PostgreSQL);
        result.Strategy.ShouldBe(DatabaseStrategy.Shared);
    }

    private static ServiceProvider BuildServiceProvider(
        HttpContext httpContext,
        bool returnErrorFromCustomerApi = false,
        IConfiguration? configuration = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });
        services.AddSingleton<IFusionCache>(new FusionCache(new FusionCacheOptions()));
        services.AddSingleton(configuration ?? new ConfigurationBuilder().Build());

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
