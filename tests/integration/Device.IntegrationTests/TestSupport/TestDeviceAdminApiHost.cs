// <copyright file="TestDeviceAdminApiHost.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Api.Endpoints.V1.DeviceDefinitions;
using Device.Application.Hanshow.Abstractions;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Infrastructure.Endpoints;

namespace Device.IntegrationTests.TestSupport;

internal sealed class TestDeviceAdminApiHost : IAsyncDisposable
{
    private readonly WebApplication app;

    private TestDeviceAdminApiHost(WebApplication app, HttpClient client)
    {
        this.app = app;
        this.Client = client;
    }

    public HttpClient Client { get; }

    public static async Task<TestDeviceAdminApiHost> StartAsync(ISender sender)
    {
        ArgumentNullException.ThrowIfNull(sender);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });

        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddOutputCache();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(TestAuthHandler.SchemeName, static _ => { });

        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, TestAuthorizationPolicyProvider>();

        builder.Services.AddSingleton(sender);
        builder.Services.AddSingleton(Substitute.For<IHanshowHeartbeatProcessor>());
        builder.Services.AddFastEndpointsInfrastructure(typeof(RegisterDeviceDefinitionEndpoint).Assembly);

        WebApplication app = builder.Build();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseFastEndpointsInfrastructure("device");

        await app.StartAsync(TestContext.Current.CancellationToken);

        return new TestDeviceAdminApiHost(app, app.GetTestClient());
    }

    public async ValueTask DisposeAsync()
    {
        this.Client.Dispose();
        await this.app.DisposeAsync();
    }
}
