// <copyright file="TestDeviceApiHost.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Device.Api.Endpoints.V1.Assignments;
using Device.Application;
using Device.Application.Assignments.Abstractions;
using Device.Application.Hanshow.Abstractions;
using DeviceDefinitionsAbstractions = Device.Application.DeviceDefinitions.Abstractions;
using Device.Application.DeviceLayouts.Abstractions;
using Device.Application.Displays.Abstractions;
using Device.Domain.AccessPoints;
using Device.Domain.Entities.DisplayAggregate;
using Device.Infrastructure.Assignments;
using FastEndpoints;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Core.Database;
using SharedKernel.Grpc.Contracts.Remote.V1.Labels;
using SharedKernel.Grpc.Contracts.Remote.V1.Products;
using SharedKernel.Infrastructure.Behaviors;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.MultiTenant;
using Wolverine;

namespace Device.IntegrationTests.TestSupport;

internal sealed class TestDeviceApiHost : IAsyncDisposable
{
    private readonly WebApplication app;

    private TestDeviceApiHost(WebApplication app, HttpClient client)
    {
        this.app = app;
        this.Client = client;
    }

    public HttpClient Client { get; }

    public static async Task<TestDeviceApiHost> StartAsync(
        string productApiRemoteAddress,
        string labelGeneratorApiRemoteAddress,
        Guid displayId,
        CancellationToken cancellationToken)
    {
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
        });

        Assembly applicationAssembly = typeof(IDeviceApplication).Assembly;
        Assembly apiAssembly = typeof(ApplyDeviceAssignmentEndpoint).Assembly;

        builder.Services.AddRouting();
        builder.Services.AddOutputCache();

        // Mock repositories to avoid EF Core dependency in integration tests
        Display display = Display.Create("TEST-001", "zone-b", Guid.NewGuid()).Value;

        var displayWriteRepo = Substitute.For<IDisplayWriteRepository>();
        displayWriteRepo.FindByIdAsync(displayId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Display?>(display));
        builder.Services.AddScoped<IDisplayWriteRepository>(_ => displayWriteRepo);

        var displayAssignmentWriteRepo = Substitute.For<IDisplayAssignmentWriteRepository>();
        builder.Services.AddScoped<IDisplayAssignmentWriteRepository>(_ => displayAssignmentWriteRepo);

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));
        builder.Services.AddScoped<IUnitOfWork>(_ => unitOfWork);

        var locationTemplateContextRunner = Substitute.For<ILocationTemplateContextRunner>();
        locationTemplateContextRunner.ResolveTemplateContextAsync("zone-b", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new LocationTemplateContextSnapshot("zone-b", "template-zone-b", "Location", 1)));
        builder.Services.AddSingleton<ILocationTemplateContextRunner>(_ => locationTemplateContextRunner);

        builder.Services.AddSingleton<IDeviceDefinitionReadRepository>(
            new TestDisplayLayoutContextRepository(displayId, maxZoneCount: 3));

        // Register remaining repository and service mocks needed for mediator handler validation.
        builder.Services.AddScoped<IAccessPointReadRepository>(_ => Substitute.For<IAccessPointReadRepository>());
        builder.Services.AddScoped<IAccessPointWriteRepository>(_ => Substitute.For<IAccessPointWriteRepository>());
        builder.Services.AddScoped<DeviceDefinitionsAbstractions.IDeviceDefinitionReadRepository>(_ => Substitute.For<DeviceDefinitionsAbstractions.IDeviceDefinitionReadRepository>());
        builder.Services.AddScoped<DeviceDefinitionsAbstractions.IDeviceDefinitionWriteRepository>(_ => Substitute.For<DeviceDefinitionsAbstractions.IDeviceDefinitionWriteRepository>());
        builder.Services.AddScoped<IDeviceLayoutReadRepository>(_ => Substitute.For<IDeviceLayoutReadRepository>());
        builder.Services.AddScoped<IDeviceLayoutWriteRepository>(_ => Substitute.For<IDeviceLayoutWriteRepository>());
        builder.Services.AddScoped<IDisplayReadRepository>(_ => Substitute.For<IDisplayReadRepository>());
        builder.Services.AddScoped<IDisplayAssignmentReadRepository>(_ => Substitute.For<IDisplayAssignmentReadRepository>());
        builder.Services.AddSingleton<IProductSnapshotRunner, InMemoryProductSnapshotRunner>();
        builder.Services.AddSingleton<ILabelRenderJobRunner, InMemoryLabelRenderJobRunner>();
        builder.Services.AddSingleton<IHanshowHeartbeatProcessor>(_ => Substitute.For<IHanshowHeartbeatProcessor>());
        builder.Services.AddSingleton<IMessageBus>(_ => Substitute.For<IMessageBus>());

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
        builder.Services.AddTeckCloudMultiTenancy();

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

        app.UseMultiTenant();
        app.UseRouting();
        app.UseFastEndpointsInfrastructure("device");

        await app.StartAsync(cancellationToken).ConfigureAwait(false);

        return new TestDeviceApiHost(app, new HttpClient
        {
            BaseAddress = new Uri(app.Urls.Single()),
        });
    }

    public async ValueTask DisposeAsync()
    {
        this.Client.Dispose();
        await this.app.DisposeAsync().ConfigureAwait(false);
    }

    private static int GetFreeTcpPort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class TestDisplayLayoutContextRepository(Guid displayId, int maxZoneCount)
        : IDeviceDefinitionReadRepository
    {
        private readonly Guid displayId = displayId;
        private readonly int maxZoneCount = maxZoneCount;

        public ValueTask<DisplayLayoutContext?> GetLayoutContextByDisplayIdAsync(Guid requestedDisplayId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (this.displayId == requestedDisplayId)
            {
                return ValueTask.FromResult<DisplayLayoutContext?>(new DisplayLayoutContext(this.displayId, Guid.NewGuid(), this.maxZoneCount));
            }

            return ValueTask.FromResult<DisplayLayoutContext?>(null);
        }
    }
}
