// <copyright file="PreviewDeviceAssignmentEndpointTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using Device.Application.Assignments.Features.PreviewDeviceAssignment.V1;
using Device.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;

#pragma warning disable CA2012

namespace Device.IntegrationTests.Endpoints.Assignments;

public sealed class PreviewDeviceAssignmentEndpointTests
{
    [Fact]
    public async Task PreviewDeviceAssignment_ShouldReturn200_WhenQuerySucceeds()
    {
        // Arrange
        var displayId = Guid.NewGuid();

        var response = new PreviewDeviceAssignmentResponse
        {
            DeviceId = displayId.ToString(),
            LocationNodeId = "shelf-a1",
            ResolvedTemplateId = "template-default",
            TemplateSource = "Ancestor",
            ZoneCount = 2,
            DuplicateProductsAllowed = true,
        };

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<PreviewDeviceAssignmentQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<PreviewDeviceAssignmentResponse>>(response));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/Assignments/Preview")
        {
            Content = JsonContent.Create(new
            {
                deviceId = displayId.ToString(),
                locationNodeId = "shelf-a1",
                zones = new[] { new { zoneIndex = 1, productId = Guid.NewGuid().ToString() } },
            }),
        };

        // Act
        HttpResponseMessage httpResponse = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PreviewDeviceAssignment_ShouldReturn422_WhenDeviceIdIsInvalidGuid()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<PreviewDeviceAssignmentQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<PreviewDeviceAssignmentResponse>>(
                Error.Validation("Device.InvalidDeviceId", "Device ID is not a valid GUID.")));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/Assignments/Preview")
        {
            Content = JsonContent.Create(new
            {
                deviceId = "not-a-guid",
                locationNodeId = "shelf-a1",
                zones = new[] { new { zoneIndex = 1, productId = Guid.NewGuid().ToString() } },
            }),
        };

        // Act
        HttpResponseMessage httpResponse = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PreviewDeviceAssignment_ShouldReturn404_WhenLayoutContextNotFound()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<PreviewDeviceAssignmentQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<PreviewDeviceAssignmentResponse>>(
                Error.NotFound("Device.LayoutNotFound", "Layout not found for this display.")));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/Assignments/Preview")
        {
            Content = JsonContent.Create(new
            {
                deviceId = Guid.NewGuid().ToString(),
                locationNodeId = "shelf-a1",
                zones = new[] { new { zoneIndex = 1, productId = Guid.NewGuid().ToString() } },
            }),
        };

        // Act
        HttpResponseMessage httpResponse = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PreviewDeviceAssignment_ShouldReturn422_WhenZoneCountExceeded()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<PreviewDeviceAssignmentQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<PreviewDeviceAssignmentResponse>>(
                Error.Validation("Device.ZoneCountExceeded", "Zone count exceeds layout maximum.")));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/Assignments/Preview")
        {
            Content = JsonContent.Create(new
            {
                deviceId = Guid.NewGuid().ToString(),
                locationNodeId = "shelf-a1",
                zones = new[]
                {
                    new { zoneIndex = 1, productId = Guid.NewGuid().ToString() },
                    new { zoneIndex = 2, productId = Guid.NewGuid().ToString() },
                    new { zoneIndex = 3, productId = Guid.NewGuid().ToString() },
                },
            }),
        };

        // Act
        HttpResponseMessage httpResponse = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}

#pragma warning restore CA2012
