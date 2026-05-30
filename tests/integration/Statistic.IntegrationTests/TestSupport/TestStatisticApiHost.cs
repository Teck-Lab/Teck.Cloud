// <copyright file="TestStatisticApiHost.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Statistic.Application;
using Statistic.Infrastructure.DependencyInjection;
using Statistic.Infrastructure.Hubs;

namespace Statistic.IntegrationTests.TestSupport;

internal sealed class TestStatisticApiHost : IAsyncDisposable
{
    private readonly WebApplication app;

    private TestStatisticApiHost(WebApplication app, HttpClient client)
    {
        this.app = app;
        this.Client = client;
    }

    public HttpClient Client { get; }

    public static async Task<TestStatisticApiHost> StartAsync()
    {
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

        builder.AddInfrastructureServices(typeof(IStatisticApplication).Assembly);
        builder.Services.AddSignalR();

        WebApplication app = builder.Build();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHub<StatisticsHub>("/hubs/statistics").RequireAuthorization();

        await app.StartAsync(TestContext.Current.CancellationToken);

        return new TestStatisticApiHost(app, app.GetTestClient());
    }

    public async ValueTask DisposeAsync()
    {
        this.Client.Dispose();
        await this.app.DisposeAsync();
    }
}
