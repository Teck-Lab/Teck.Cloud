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

    [Fact]
    public void ResolveTenantConnection_UsesSecretFileBeforeConfiguration_WhenDedicatedTenant()
    {
        // Arrange
        string tenantId = $"tenant-{Guid.NewGuid():N}";
        string secretDir = Path.Combine(Path.GetTempPath(), "tenant-secrets", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(secretDir);

        try
        {
            string secretKey = $"ConnectionStrings__Tenants__{tenantId}__Write";
            string secretPath = Path.Combine(secretDir, secretKey);
            File.WriteAllText(secretPath, "Host=file-write;Username=file;");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TenantConnectionSecrets:Directory"] = secretDir,
                    [$"ConnectionStrings:Tenants:{tenantId}:Write"] = "Host=config-write;Username=config;",
                    [$"ConnectionStrings:Tenants:{tenantId}:Read"] = "Host=config-read;Username=config;",
                })
                .Build();

            ServiceProvider serviceProvider = BuildServiceProvider(new DefaultHttpContext(), configuration: configuration);
            var resolver = new TenantDbConnectionResolver(
                serviceProvider,
                "Host=shared-write;",
                "Host=shared-read;",
                DatabaseProvider.PostgreSQL);

            var tenant = new TenantDetails
            {
                Id = tenantId,
                DatabaseStrategy = "Dedicated",
                DatabaseProvider = "PostgreSQL",
                HasReadReplicas = true,
            };

            // Act
            var result = resolver.ResolveTenantConnection(tenant);

            // Assert
            result.WriteConnectionString.ShouldBe("Host=file-write;Username=file;");
            result.ReadConnectionString.ShouldBe("Host=config-read;Username=config;");
        }
        finally
        {
            if (Directory.Exists(secretDir))
            {
                Directory.Delete(secretDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolveTenantConnection_UsesConfigurationKeys_WhenDedicatedTenantHasNoInlineConnections()
    {
        // Arrange
        string tenantId = $"tenant-{Guid.NewGuid():N}";
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:Tenants:{tenantId}:Write"] = "Host=config-write;",
                [$"ConnectionStrings:Tenants:{tenantId}:Read"] = "Host=config-read;",
            })
            .Build();

        ServiceProvider serviceProvider = BuildServiceProvider(new DefaultHttpContext(), configuration: configuration);
        var resolver = new TenantDbConnectionResolver(
            serviceProvider,
            "Host=shared-write;",
            "Host=shared-read;",
            DatabaseProvider.PostgreSQL);

        var tenant = new TenantDetails
        {
            Id = tenantId,
            DatabaseStrategy = "Dedicated",
            DatabaseProvider = "PostgreSQL",
            HasReadReplicas = true,
        };

        // Act
        var result = resolver.ResolveTenantConnection(tenant);

        // Assert
        result.WriteConnectionString.ShouldBe("Host=config-write;");
        result.ReadConnectionString.ShouldBe("Host=config-read;");
    }

    [Fact]
    public void ResolveTenantConnection_UsesEnvironmentFallback_WhenConfigurationIsMissing()
    {
        // Arrange
        string tenantId = $"tenant-{Guid.NewGuid():N}";
        string writeEnvKey = $"ConnectionStrings__Tenants__{tenantId}__Write";
        string readEnvKey = $"ConnectionStrings__Tenants__{tenantId}__Read";

        Environment.SetEnvironmentVariable(writeEnvKey, "Host=env-write;");
        Environment.SetEnvironmentVariable(readEnvKey, "Host=env-read;");

        try
        {
            ServiceProvider serviceProvider = BuildServiceProvider(new DefaultHttpContext(), configuration: new ConfigurationBuilder().Build());
            var resolver = new TenantDbConnectionResolver(
                serviceProvider,
                "Host=shared-write;",
                "Host=shared-read;",
                DatabaseProvider.PostgreSQL);

            var tenant = new TenantDetails
            {
                Id = tenantId,
                DatabaseStrategy = "Dedicated",
                DatabaseProvider = "PostgreSQL",
                HasReadReplicas = true,
            };

            // Act
            var result = resolver.ResolveTenantConnection(tenant);

            // Assert
            result.WriteConnectionString.ShouldBe("Host=env-write;");
            result.ReadConnectionString.ShouldBe("Host=env-read;");
        }
        finally
        {
            Environment.SetEnvironmentVariable(writeEnvKey, null);
            Environment.SetEnvironmentVariable(readEnvKey, null);
        }
    }

    [Fact]
    public void ResolveTenantConnection_UsesTenantSpecificReadConnection_WhenSharedTenantHasSeparateRead()
    {
        // Arrange
        string tenantId = $"tenant-{Guid.NewGuid():N}";
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:Tenants:{tenantId}:Read"] = "Host=tenant-shared-read;",
            })
            .Build();

        ServiceProvider serviceProvider = BuildServiceProvider(new DefaultHttpContext(), configuration: configuration);
        var resolver = new TenantDbConnectionResolver(
            serviceProvider,
            "Host=shared-write;",
            "Host=shared-read;",
            DatabaseProvider.PostgreSQL);

        var tenant = new TenantDetails
        {
            Id = tenantId,
            DatabaseStrategy = "Shared",
            DatabaseProvider = "PostgreSQL",
            HasReadReplicas = true,
        };

        // Act
        var result = resolver.ResolveTenantConnection(tenant);

        // Assert
        result.Strategy.ShouldBe(DatabaseStrategy.Shared);
        result.WriteConnectionString.ShouldBe("Host=shared-write;");
        result.ReadConnectionString.ShouldBe("Host=tenant-shared-read;");
    }

    [Fact]
    public void ResolveTenantConnection_Throws_WhenMySqlSeparateReadIsConfiguredWithoutReadConnection()
    {
        // Arrange
        string tenantId = $"tenant-{Guid.NewGuid():N}";
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:Tenants:{tenantId}:Write"] = "Server=mysql-write;",
            })
            .Build();

        ServiceProvider serviceProvider = BuildServiceProvider(new DefaultHttpContext(), configuration: configuration);
        var resolver = new TenantDbConnectionResolver(
            serviceProvider,
            "Host=shared-write;",
            "Host=shared-read;",
            DatabaseProvider.PostgreSQL);

        var tenant = new TenantDetails
        {
            Id = tenantId,
            DatabaseStrategy = "Dedicated",
            DatabaseProvider = "MySQL",
            HasReadReplicas = true,
        };

        // Act
        Should.Throw<InvalidOperationException>(() => resolver.ResolveTenantConnection(tenant));
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
