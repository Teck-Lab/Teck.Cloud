// <copyright file="DisplayEndpointAuthTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using Device.Application.Displays.Features.AddDisplays.V1;
using Device.Application.Displays.Features.GetDisplays.V1;
using Device.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;

#pragma warning disable CA2012

namespace Device.IntegrationTests.Endpoints.Auth;

public sealed class DisplayEndpointAuthTests
{
    [Fact]
    public async Task AddDisplays_ShouldReturn401_WhenNoAuthorizationHeader()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/Displays/Batch")
        {
            Content = JsonContent.Create(new
            {
                locationNodeId = "shelf-a1",
                displays = new[] { new { shortSerial = "AE-6F-B8-87" } },
            }),
        };

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddDisplays_ShouldReturn403_WhenAuthenticatedButMissingScopeClaim()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/Displays/Batch")
        {
            Content = JsonContent.Create(new
            {
                locationNodeId = "shelf-a1",
                displays = new[] { new { shortSerial = "AE-6F-B8-87" } },
            }),
        };
        request.WithAuthenticatedUser();

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddDisplays_ShouldReturn200_WhenAuthenticatedWithCorrectScope()
    {
        // Arrange
        var addResponse = new AddDisplaysResponse(
            Results: [new AddDisplayResult("AE-6F-B8-87", Guid.NewGuid(), Duplicate: false)],
            AddedCount: 1,
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
                displays = new[] { new { shortSerial = "AE-6F-B8-87" } },
            }),
        };
        request.WithAuthenticatedUser().WithScopes("display:create");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDisplays_ShouldReturn401_WhenNoAuthorizationHeader()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, "/device/v1/Displays?locationNodeId=shelf-a1");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDisplays_ShouldReturn403_WhenAuthenticatedButMissingScopeClaim()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, "/device/v1/Displays?locationNodeId=shelf-a1");
        request.WithAuthenticatedUser();

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetDisplays_ShouldReturn200_WhenAuthenticatedWithCorrectScope()
    {
        // Arrange
        IReadOnlyList<GetDisplayItemResponse> items = new List<GetDisplayItemResponse>();

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetDisplaysQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<IReadOnlyList<GetDisplayItemResponse>>>(
                ErrorOrFactory.From(items)));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, "/device/v1/Displays?locationNodeId=shelf-a1");
        request.WithAuthenticatedUser().WithScopes("display:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

}

#pragma warning restore CA2012
