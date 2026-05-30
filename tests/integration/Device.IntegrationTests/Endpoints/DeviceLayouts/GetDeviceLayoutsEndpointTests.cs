// <copyright file="GetDeviceLayoutsEndpointTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using Device.Application.DeviceLayouts.Features.GetDeviceLayouts.V1;
using Device.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;
#pragma warning disable CA2012

namespace Device.IntegrationTests.Endpoints.DeviceLayouts;

public sealed class GetDeviceLayoutsEndpointTests
{
    [Fact]
    public async Task GetDeviceLayouts_ShouldReturn200_WithLayouts_WhenLayoutsExistForDefinition()
    {
        // Arrange
        Guid definitionId = Guid.NewGuid();
        Guid layoutIdOne = Guid.NewGuid();
        Guid layoutIdTwo = Guid.NewGuid();
        GetDeviceLayoutsQuery? capturedQuery = null;

        IReadOnlyList<GetDeviceLayoutItemResponse> layouts =
        [
            new(layoutIdOne, definitionId, "Full Screen", 1),
            new(layoutIdTwo, definitionId, "Split Screen", 2),
        ];

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetDeviceLayoutsQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedQuery = callInfo.Arg<GetDeviceLayoutsQuery>();
                return new ValueTask<ErrorOr<IReadOnlyList<GetDeviceLayoutItemResponse>>>(ErrorOrFactory.From<IReadOnlyList<GetDeviceLayoutItemResponse>>(layouts));
            });

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        // Act
        HttpResponseMessage response = await host.Client.GetAsync(
            new Uri($"/device/v1/DeviceLayouts?deviceDefinitionId={definitionId}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedQuery.ShouldNotBeNull();
        capturedQuery.DeviceDefinitionId.ShouldBe(definitionId);

        IReadOnlyList<GetDeviceLayoutItemResponse>? body = await response.Content
            .ReadFromJsonAsync<IReadOnlyList<GetDeviceLayoutItemResponse>>(cancellationToken: TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Count.ShouldBe(2);
        body[0].Name.ShouldBe("Full Screen");
        body[1].Name.ShouldBe("Split Screen");
    }

    [Fact]
    public async Task GetDeviceLayouts_ShouldReturn200_WithEmptyList_WhenNoLayoutsExist()
    {
        // Arrange
        Guid definitionId = Guid.NewGuid();

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetDeviceLayoutsQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                IReadOnlyList<GetDeviceLayoutItemResponse> empty = Array.Empty<GetDeviceLayoutItemResponse>();
                return new ValueTask<ErrorOr<IReadOnlyList<GetDeviceLayoutItemResponse>>>(ErrorOrFactory.From<IReadOnlyList<GetDeviceLayoutItemResponse>>(empty));
            });

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        // Act
        HttpResponseMessage response = await host.Client.GetAsync(
            new Uri($"/device/v1/DeviceLayouts?deviceDefinitionId={definitionId}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        IReadOnlyList<GetDeviceLayoutItemResponse>? body = await response.Content
            .ReadFromJsonAsync<IReadOnlyList<GetDeviceLayoutItemResponse>>(cancellationToken: TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Count.ShouldBe(0);
    }
}
