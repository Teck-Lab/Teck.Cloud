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
using Wolverine;

namespace Device.IntegrationTests.TestSupport;

internal sealed class TestDeviceApiHostWithMessaging : IAsyncDisposable
{
    // Serializes concurrent MigrateAsync calls across test hosts sharing the same Postgres container.
    // Repository tests in the same collection call EnsureDeletedAsync which wipes the schema;
    // without serialization two StartAsync calls can race to apply InitialDeviceSchema simultaneously.
    private static readonly SemaphoreSlim MigrationGate = new(1, 1);

    private readonly WebApplication app;
    private readonly List<IServiceScope> scopes = new();

    private TestDeviceApiHostWithMessaging(WebApplication app, HttpClient client)
    {
        this.app = app;
        this.Client = client;
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
        string postgresConnectionString,
        string rabbitConnectionString,
        string productApiRemoteAddress,
        string labelGeneratorApiRemoteAddress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(postgresConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(rabbitConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(productApiRemoteAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelGeneratorApiRemoteAddress);

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
            ["ConnectionStrings:db-write"] = postgresConnectionString,
            ["ConnectionStrings:db-read"] = postgresConnectionString,
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
                .UseNpgsql(postgresConnectionString, npgsql => npgsql.MigrationsAssembly("Teck.Cloud.Migrations.PostgreSQL"))
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

        // Re-apply EF schema as a safety net after schema wipes by EnsureDeletedAsync / EnsureCreatedAsync
        // in repo tests.  MUST run before store.Admin.MigrateAsync() so that EnsureDeletedAsync (in the
        // else branch) does not terminate Wolverine's already-open connections.
        //   Case 1: DB is clean (migrations ran) → MigrateAsync is a no-op.
        //   Case 2: A repo test called EnsureCreatedAsync on the *read* context so __EFMigrationsHistory
        //      does not exist and only read-model tables are present.  EnsureDeletedAsync + MigrateAsync
        //      tears down the partial schema and rebuilds it fully from migrations.
        await MigrationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dbContextOptions = new DbContextOptionsBuilder<Device.Infrastructure.Persistence.DeviceWriteDbContext>()
                .UseNpgsql(postgresConnectionString, npgsql => npgsql.MigrationsAssembly("Teck.Cloud.Migrations.PostgreSQL"))
                .Options;
            await using var dbContext = new Device.Infrastructure.Persistence.DeviceWriteDbContext(dbContextOptions);

            // GetAppliedMigrationsAsync queries __EFMigrationsHistory; it returns an empty
            // enumerable (not an exception) when the table does not exist.
            IEnumerable<string> applied = await dbContext.Database
                .GetAppliedMigrationsAsync(cancellationToken)
                .ConfigureAwait(false);
            if (applied.Any())
            {
                await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // No migration history: a repo test called EnsureCreatedAsync on the read context,
                // leaving the DB in a partial EnsureCreated state (only read-model tables present).
                // EnsureCreatedAsync on the write context would be a no-op because the DB already
                // exists with tables, so EF Core assumes the schema is current.
                // Delete and re-migrate to get a fully correct write-model schema.
                await dbContext.Database.EnsureDeletedAsync(cancellationToken).ConfigureAwait(false);
                await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            MigrationGate.Release();
        }

        var store = app.Services.GetRequiredService<Wolverine.Persistence.Durability.IMessageStore>();
        await store.Admin.MigrateAsync().ConfigureAwait(false);

        await app.StartAsync(cancellationToken).ConfigureAwait(false);

        return new TestDeviceApiHostWithMessaging(app, new HttpClient
        {
            BaseAddress = new Uri(app.Urls.Single()),
        });
    }

    public async ValueTask DisposeAsync()
    {
        foreach (IServiceScope scope in this.scopes)
        {
            scope.Dispose();
        }

        this.scopes.Clear();
        this.Client.Dispose();
        await this.app.DisposeAsync().ConfigureAwait(false);
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
