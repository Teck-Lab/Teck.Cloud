// <copyright file="AddDisplaysEndpointTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using Device.Application.Displays.Features.AddDisplays.V1;
using Device.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;

#pragma warning disable CA2012

namespace Device.IntegrationTests.Endpoints.Displays;

public sealed class AddDisplaysEndpointTests
{
    [Fact]
    public async Task AddDisplays_ShouldReturn200_WithAddedCount_WhenCommandSucceeds()
    {
        // Arrange
        var displayIdOne = Guid.NewGuid();
        var displayIdTwo = Guid.NewGuid();

        var addResponse = new AddDisplaysResponse(
            Results:
            [
                new AddDisplayResult("AE-6F-B8-87", displayIdOne, Duplicate: false),
                new AddDisplayResult("00-11-22-33", displayIdTwo, Duplicate: false),
            ],
            AddedCount: 2,
            DuplicateCount: 0);

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<AddDisplaysCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<AddDisplaysResponse>>(addResponse));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/Displays/Batch")
        {
            Content = JsonContent.Create(new
            {
                locationNodeId = "shelf-a1",
                displays = new[]
                {
                    new { shortSerial = "AE-6F-B8-87" },
                    new { shortSerial = "00-11-22-33" },
                },
            }),
        };

        request.Headers.Add("Authorization", "Bearer test");
        request.Headers.Add(TestAuthHandler.ScopeHeaderName, "display:create");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AddDisplaysResponse? body = await response.Content
            .ReadFromJsonAsync<AddDisplaysResponse>(cancellationToken: TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body!.AddedCount.ShouldBe(2);
        body.DuplicateCount.ShouldBe(0);
        body.Results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddDisplays_ShouldReturn200_WithDuplicateCount_WhenSomeSerialsAlreadyExist()
    {
        // Arrange
        var displayIdNew = Guid.NewGuid();

        var addResponse = new AddDisplaysResponse(
            Results:
            [
                new AddDisplayResult("AE-6F-B8-87", displayIdNew, Duplicate: false),
                new AddDisplayResult("00-11-22-33", DisplayId: null, Duplicate: true),
            ],
            AddedCount: 1,
            DuplicateCount: 1);

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<AddDisplaysCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<AddDisplaysResponse>>(addResponse));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/Displays/Batch")
        {
            Content = JsonContent.Create(new
            {
                locationNodeId = "shelf-a1",
                displays = new[]
                {
                    new { shortSerial = "AE-6F-B8-87" },
                    new { shortSerial = "00-11-22-33" },
                },
            }),
        };

        request.Headers.Add("Authorization", "Bearer test");
        request.Headers.Add(TestAuthHandler.ScopeHeaderName, "display:create");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AddDisplaysResponse? body = await response.Content
            .ReadFromJsonAsync<AddDisplaysResponse>(cancellationToken: TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body!.AddedCount.ShouldBe(1);
        body.DuplicateCount.ShouldBe(1);
    }
}

#pragma warning restore CA2012
