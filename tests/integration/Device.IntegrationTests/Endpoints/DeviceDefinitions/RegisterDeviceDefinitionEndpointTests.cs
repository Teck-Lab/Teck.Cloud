// <copyright file="RegisterDeviceDefinitionEndpointTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using Device.Application.DeviceDefinitions.Features.RegisterDeviceDefinition.V1;
using Device.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;
#pragma warning disable CA2012

namespace Device.IntegrationTests.Endpoints.DeviceDefinitions;

public sealed class RegisterDeviceDefinitionEndpointTests
{
    [Fact]
    public async Task RegisterDeviceDefinition_ShouldReturn200_WithNewId_WhenCommandSucceeds()
    {
        // Arrange
        Guid newId = Guid.NewGuid();

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<RegisterDeviceDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<RegisterDeviceDefinitionResponse>>(new RegisterDeviceDefinitionResponse(newId)));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/DeviceDefinitions")
        {
            Content = JsonContent.Create(new
            {
                modelId = "HS-SE2130R",
                name = "Hanshow 2.13\" Red",
                eslProvider = "Hanshow",
                supportedColors = 1,
                supportsNfc = false,
            }),
        };

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        RegisterDeviceDefinitionResponse? body = await response.Content
            .ReadFromJsonAsync<RegisterDeviceDefinitionResponse>(cancellationToken: TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Id.ShouldBe(newId);
    }

    [Fact]
    public async Task RegisterDeviceDefinition_ShouldReturn400_WhenEslProviderIsInvalid()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/DeviceDefinitions")
        {
            Content = JsonContent.Create(new
            {
                modelId = "HS-SE2130R",
                name = "Hanshow 2.13\" Red",
                eslProvider = "InvalidProvider",
                supportedColors = 1,
                supportsNfc = false,
            }),
        };

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await sender.DidNotReceive().Send(Arg.Any<RegisterDeviceDefinitionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterDeviceDefinition_ShouldReturn409_WhenModelIdIsAlreadyRegistered()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<RegisterDeviceDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<RegisterDeviceDefinitionResponse>>(
                Error.Conflict("DeviceDefinition.DuplicateModelId", "A device definition with this model ID already exists.")));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/DeviceDefinitions")
        {
            Content = JsonContent.Create(new
            {
                modelId = "HS-SE2130R",
                name = "Hanshow 2.13\" Red",
                eslProvider = "Hanshow",
                supportedColors = 1,
                supportsNfc = false,
            }),
        };

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
