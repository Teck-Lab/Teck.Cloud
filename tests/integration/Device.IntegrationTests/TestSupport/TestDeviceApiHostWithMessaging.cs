// <copyright file="TestDeviceApiHostWithMessaging.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Device.Api.Endpoints.V1.Assignments;
using Device.Application;
using Device.Application.Assignments.Abstractions;
using Device.Infrastructure.DependencyInjection;
using FastEndpoints;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Grpc.Contracts.Remote.V1.Labels;
using SharedKernel.Grpc.Contracts.Remote.V1.Products;
using SharedKernel.Infrastructure.Behaviors;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.MultiTenant;
using Teck.Cloud.IntegrationTests.Shared;
using Wolverine;

namespace Device.IntegrationTests.TestSupport;

internal sealed class TestDeviceApiHostWithMessaging : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly List<IServiceScope> scopes = new();
    private readonly string dbConnectionString;
    private readonly SharedTestcontainersFixture sharedFixture;

    private TestDeviceApiHostWithMessaging(WebApplication app, HttpClient client, string dbConnectionString, SharedTestcontainersFixture sharedFixture)
    {
        this.app = app;
        this.Client = client;
        this.dbConnectionString = dbConnectionString;
        this.sharedFixture = sharedFixture;
    }

    public HttpClient Client { get; }

    public IServiceProvider Services => this.app.Services;

    public IMessageBus MessageBus
    {
        get
        {
            // Wolverine's IMessageBus is scoped; resolve from a fresh scope to honor DI lifetime.
            // The scope is retained on the host and disposed in DisposeAsync to avoid leaks.
            IServiceScope scope = this.app.Services.CreateScope();
            this.scopes.Add(scope);
            return scope.ServiceProvider.GetRequiredService<IMessageBus>();
        }
    }

    public static async Task<TestDeviceApiHostWithMessaging> StartAsync(
        SharedTestcontainersFixture sharedFixture,
        string productApiRemoteAddress,
        string labelGeneratorApiRemoteAddress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productApiRemoteAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelGeneratorApiRemoteAddress);

        // Create or reuse a shared test database for this host
        string dbConnectionString = await sharedFixture.CreateSharedTestDatabaseAsync(
            typeof(Device.Infrastructure.Persistence.DeviceWriteDbContext),
            "Teck.Cloud.Migrations.PostgreSQL",
            cancellationToken);

        string rabbitConnectionString = sharedFixture.RabbitMqConnectionString;

        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });

        var port = GetFreeTcpPort();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Services:ProductApi:Url"] = productApiRemoteAddress,
            ["Services:LabelGeneratorApi:Url"] = labelGeneratorApiRemoteAddress,
            ["ConnectionStrings:db-write"] = dbConnectionString,
            ["ConnectionStrings:db-read"] = dbConnectionString,
            ["ConnectionStrings:rabbitmq"] = rabbitConnectionString,
            ["Database:Provider"] = "postgres",
        });

        Assembly applicationAssembly = typeof(IDeviceApplication).Assembly;
        Assembly apiAssembly = typeof(ApplyDeviceAssignmentEndpoint).Assembly;

        builder.Services.AddRouting();
        builder.Services.AddOutputCache();

        builder.AddInfrastructureServices(applicationAssembly);
        builder.Services.AddSingleton<IDeviceDefinitionReadRepository>(
            new TestDisplayLayoutContextRepository(maxZoneCount: 3));
        builder.Services.AddSingleton<ILocationTemplateContextRunner, TestLocationTemplateContextRunner>();

        // Override Finbuckle's per-scope tenant accessor with a fixed test accessor.
        // Must be registered AFTER AddInfrastructureServices so the last DI registration wins,
        // otherwise AddTeckCloudMultiTenancy's MultiTenantContextAccessor<TenantDetails> takes
        // precedence and returns a null TenantInfo because UseMultiTenant() middleware is absent.
        FixedTenantContextAccessor tenantAccessor = new();
        builder.Services.AddSingleton<IMultiTenantContextAccessor<TenantDetails>>(tenantAccessor);
        builder.Services.AddSingleton<IMultiTenantContextAccessor>(tenantAccessor);

        // Override the Wolverine-managed DbContext registration so that HTTP-scoped requests
        // (FastEndpoints → MediatR, no Wolverine envelope) get a properly tenanted context.
        // AddDbContextWithWolverineManagedMultiTenancy uses Activator.CreateInstance(options)
        // which bypasses DI and therefore never injects the tenant accessor; for HTTP paths the
        // envelope tenant is also absent, so TenantId resolves to null and query filters match
        // nothing. Registering a Scoped override here wins because it is added last.
        builder.Services.AddScoped<Device.Infrastructure.Persistence.DeviceWriteDbContext>(sp =>
        {
            var accessor = sp.GetRequiredService<IMultiTenantContextAccessor<TenantDetails>>();
            var opts = new DbContextOptionsBuilder<Device.Infrastructure.Persistence.DeviceWriteDbContext>()
                .UseNpgsql(dbConnectionString, npgsql => npgsql.MigrationsAssembly("Teck.Cloud.Migrations.PostgreSQL"))
                .Options;
            return new Device.Infrastructure.Persistence.DeviceWriteDbContext(opts, accessor);
        });

        builder.Services.AddFastEndpointsInfrastructure(applicationAssembly, apiAssembly);
        builder.Services.AddMediator(static options =>
        {
            options.Assemblies = [typeof(IDeviceApplication)];
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors =
            [
                typeof(LoggingBehavior<,>),
            ];
        });

        WebApplication app = builder.Build();

        app.MapRemote(
            productApiRemoteAddress,
            remote =>
            {
                remote.Register<GetProductSnapshotsCommand, GetProductSnapshotsRpcResult>();
            });

        app.MapRemote(
            labelGeneratorApiRemoteAddress,
            remote =>
            {
                remote.Register<EnqueueRenderJobCommand, EnqueueRenderJobRpcResult>();
            });

        app.UseRouting();
        app.UseFastEndpointsInfrastructure("device");

        var store = app.Services.GetRequiredService<Wolverine.Persistence.Durability.IMessageStore>();
        await store.Admin.MigrateAsync();

        await app.StartAsync(cancellationToken);

        return new TestDeviceApiHostWithMessaging(app, new HttpClient
        {
            BaseAddress = new Uri(app.Urls.Single()),
        }, dbConnectionString, sharedFixture);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (IServiceScope scope in this.scopes)
        {
            scope.Dispose();
        }

        this.scopes.Clear();
        this.Client.Dispose();
        await this.app.DisposeAsync();

        // Truncate the shared test database for isolation
        try
        {
            await this.sharedFixture.TruncateAllTablesAsync(this.dbConnectionString, TestContext.Current.CancellationToken);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private static int GetFreeTcpPort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class TestDisplayLayoutContextRepository(int maxZoneCount)
        : IDeviceDefinitionReadRepository
    {
        private readonly int maxZoneCount = maxZoneCount;

        public ValueTask<DisplayLayoutContext?> GetLayoutContextByDisplayIdAsync(Guid requestedDisplayId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult<DisplayLayoutContext?>(new DisplayLayoutContext(requestedDisplayId, Guid.NewGuid(), this.maxZoneCount));
        }
    }

    private sealed class TestLocationTemplateContextRunner : ILocationTemplateContextRunner
    {
        public ValueTask<LocationTemplateContextSnapshot> ResolveTemplateContextAsync(
            string locationNodeId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(new LocationTemplateContextSnapshot(
                LocationNodeId: locationNodeId,
                ResolvedTemplateId: "test-template",
                TemplateSource: "Explicit",
                AncestorDepthScanned: 0,
                ResolvedTemplateDesign: new ResolvedTemplateDesignSnapshot(
                    TemplateId: "test-template",
                    Name: "Test Template",
                    Width: 296,
                    Height: 128,
                    BackgroundColor: "#FFFFFF",
                    ElementsJson: "[]",
                    DefaultsJson: "{}")));
        }
    }
}
