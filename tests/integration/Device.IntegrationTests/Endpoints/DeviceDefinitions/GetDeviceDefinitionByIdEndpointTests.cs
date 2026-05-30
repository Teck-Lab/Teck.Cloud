// <copyright file="GetDeviceDefinitionByIdEndpointTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using Device.Application.DeviceDefinitions.Features.GetDeviceDefinitionById.V1;
using Device.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;
#pragma warning disable CA2012

namespace Device.IntegrationTests.Endpoints.DeviceDefinitions;

public sealed class GetDeviceDefinitionByIdEndpointTests
{
    [Fact]
    public async Task GetDeviceDefinitionById_ShouldReturn200_WithDefinition_WhenDefinitionExists()
    {
        // Arrange
        Guid definitionId = Guid.NewGuid();
        GetDeviceDefinitionByIdQuery? capturedQuery = null;

        GetDeviceDefinitionByIdResponse expectedResponse = new(
            definitionId,
            "HS-SE2130R",
            "Hanshow 2.13\" Red",
            250,
            122,
            1,
            false,
            "Hanshow",
            null,
            null,
            null);

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetDeviceDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedQuery = callInfo.Arg<GetDeviceDefinitionByIdQuery>();
                return new ValueTask<ErrorOr<GetDeviceDefinitionByIdResponse>>(expectedResponse);
            });

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        // Act
        HttpResponseMessage response = await host.Client.GetAsync(
            new Uri($"/device/v1/DeviceDefinitions/{definitionId}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedQuery.ShouldNotBeNull();
        capturedQuery.Id.ShouldBe(definitionId);

        GetDeviceDefinitionByIdResponse? body = await response.Content
            .ReadFromJsonAsync<GetDeviceDefinitionByIdResponse>(cancellationToken: TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Id.ShouldBe(definitionId);
        body.ModelId.ShouldBe("HS-SE2130R");
        body.EslProvider.ShouldBe("Hanshow");
    }

    [Fact]
    public async Task GetDeviceDefinitionById_ShouldReturn404_WhenDefinitionDoesNotExist()
    {
        // Arrange
        Guid definitionId = Guid.NewGuid();

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetDeviceDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<GetDeviceDefinitionByIdResponse>>(
                Error.NotFound("DeviceDefinition.NotFound", "Device definition not found.")));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        // Act
        HttpResponseMessage response = await host.Client.GetAsync(
            new Uri($"/device/v1/DeviceDefinitions/{definitionId}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
