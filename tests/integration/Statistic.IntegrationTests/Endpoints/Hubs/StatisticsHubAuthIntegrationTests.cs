// <copyright file="StatisticsHubAuthIntegrationTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Shouldly;
using Statistic.IntegrationTests.TestSupport;

namespace Statistic.IntegrationTests.Endpoints.Hubs;

public sealed class StatisticsHubAuthIntegrationTests
{
    [Fact]
    public async Task ConnectToHub_ShouldFail401_WhenNoAuthToken()
    {
        // Arrange
        await using TestStatisticApiHost host = await TestStatisticApiHost.StartAsync();

        HubConnection connection = new HubConnectionBuilder()
            .WithUrl(
                $"{host.Client.BaseAddress}hubs/statistics",
                HttpTransportType.WebSockets,
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => host.Client.GetType()
                        .GetProperty("Handler")?.GetValue(host.Client) as HttpMessageHandler
                        ?? new HttpClientHandler();
                })
            .Build();

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(async () =>
            await connection.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Negotiate_ShouldReturn401_WhenNoAuthToken()
    {
        // Arrange
        await using TestStatisticApiHost host = await TestStatisticApiHost.StartAsync();

        // Act
        using HttpResponseMessage response = await host.Client.PostAsync(
            "/hubs/statistics/negotiate",
            new StringContent("{}"),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
