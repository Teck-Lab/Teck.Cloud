// <copyright file="CreateDeviceLayoutEndpointTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using Device.Application.DeviceLayouts.Features.CreateDeviceLayout.V1;
using Device.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;
#pragma warning disable CA2012

namespace Device.IntegrationTests.Endpoints.DeviceLayouts;

public sealed class CreateDeviceLayoutEndpointTests
{
    [Fact]
    public async Task CreateDeviceLayout_ShouldReturn200_WithNewId_WhenCommandSucceeds()
    {
        // Arrange
        Guid definitionId = Guid.NewGuid();
        Guid newLayoutId = Guid.NewGuid();
        CreateDeviceLayoutCommand? capturedCommand = null;

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<CreateDeviceLayoutCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedCommand = callInfo.Arg<CreateDeviceLayoutCommand>();
                return new ValueTask<ErrorOr<CreateDeviceLayoutResponse>>(new CreateDeviceLayoutResponse(newLayoutId));
            });

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/DeviceLayouts")
        {
            Content = JsonContent.Create(new
            {
                deviceDefinitionId = definitionId,
                name = "Full Screen",
                maxZoneCount = 1,
            }),
        };

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedCommand.ShouldNotBeNull();
        capturedCommand.DeviceDefinitionId.ShouldBe(definitionId);
        capturedCommand.Name.ShouldBe("Full Screen");
        capturedCommand.MaxZoneCount.ShouldBe(1);

        CreateDeviceLayoutResponse? body = await response.Content
            .ReadFromJsonAsync<CreateDeviceLayoutResponse>(cancellationToken: TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Id.ShouldBe(newLayoutId);
    }

    [Fact]
    public async Task CreateDeviceLayout_ShouldReturn404_WhenDefinitionDoesNotExist()
    {
        // Arrange
        Guid definitionId = Guid.NewGuid();

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<CreateDeviceLayoutCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<CreateDeviceLayoutResponse>>(
                Error.NotFound("DeviceDefinition.NotFound", "Device definition not found.")));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/DeviceLayouts")
        {
            Content = JsonContent.Create(new
            {
                deviceDefinitionId = definitionId,
                name = "Full Screen",
                maxZoneCount = 1,
            }),
        };

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
