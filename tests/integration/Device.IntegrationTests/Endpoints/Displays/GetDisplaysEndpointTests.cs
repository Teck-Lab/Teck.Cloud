// <copyright file="GetDisplaysEndpointTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using Device.Application.Displays.Features.GetDisplays.V1;
using Device.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;

#pragma warning disable CA2012

namespace Device.IntegrationTests.Endpoints.Displays;

public sealed class GetDisplaysEndpointTests
{
    [Fact]
    public async Task GetDisplays_ShouldReturn200_WithDisplayList_WhenQuerySucceeds()
    {
        // Arrange
        var displayIdOne = Guid.NewGuid();
        var displayIdTwo = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        IReadOnlyList<GetDisplayItemResponse> items = new List<GetDisplayItemResponse>
        {
            new(displayIdOne, "AE-6F-B8-87", 229582052926557319L, "shelf-a1", null, now),
            new(displayIdTwo, "00-11-22-33", null, "shelf-a1", null, now.AddMinutes(-5)),
        };

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetDisplaysQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<IReadOnlyList<GetDisplayItemResponse>>>(
                ErrorOrFactory.From(items)));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, "/device/v1/Displays?locationNodeId=shelf-a1");
        request.Headers.Add("Authorization", "Bearer test");
        request.Headers.Add(TestAuthHandler.ScopeHeaderName, "display:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<GetDisplayItemResponse>? body = await response.Content
            .ReadFromJsonAsync<List<GetDisplayItemResponse>>(cancellationToken: TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body!.Count.ShouldBe(2);
        body[0].ShortSerial.ShouldBe("AE-6F-B8-87");
        body[1].ShortSerial.ShouldBe("00-11-22-33");
    }

    [Fact]
    public async Task GetDisplays_ShouldReturn200_WithEmptyList_WhenNoDisplaysExistForLocation()
    {
        // Arrange
        IReadOnlyList<GetDisplayItemResponse> items = new List<GetDisplayItemResponse>();

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetDisplaysQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<IReadOnlyList<GetDisplayItemResponse>>>(
                ErrorOrFactory.From(items)));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, "/device/v1/Displays?locationNodeId=empty-node");
        request.Headers.Add("Authorization", "Bearer test");
        request.Headers.Add(TestAuthHandler.ScopeHeaderName, "display:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<GetDisplayItemResponse>? body = await response.Content
            .ReadFromJsonAsync<List<GetDisplayItemResponse>>(cancellationToken: TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body!.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetDisplays_ShouldPassLocationNodeId_ToQuery()
    {
        // Arrange
        IReadOnlyList<GetDisplayItemResponse> items = new List<GetDisplayItemResponse>();
        GetDisplaysQuery? capturedQuery = null;

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Do<GetDisplaysQuery>(q => capturedQuery = q), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<IReadOnlyList<GetDisplayItemResponse>>>(
                ErrorOrFactory.From(items)));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, "/device/v1/Displays?locationNodeId=zone-b2");
        request.Headers.Add("Authorization", "Bearer test");
        request.Headers.Add(TestAuthHandler.ScopeHeaderName, "display:list");

        // Act
        await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        capturedQuery.ShouldNotBeNull();
        capturedQuery!.LocationNodeId.ShouldBe("zone-b2");
    }
}

#pragma warning restore CA2012
