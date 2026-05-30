// <copyright file="DeviceAssignmentEdgeCaseTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using Device.Application.Assignments.Features.ApplyDeviceAssignment.V1;
using Device.Application.Assignments.Features.PreviewDeviceAssignment.V1;
using Device.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;

#pragma warning disable CA2012

namespace Device.IntegrationTests.CrossService;

public sealed class DeviceAssignmentEdgeCaseTests
{
    [Fact]
    public async Task ApplyDeviceAssignment_ShouldReturn200_WhenZonesAreAtMaxCount()
    {
        // Arrange
        var responseData = new ApplyDeviceAssignmentResponse
        {
            DeviceId = "disp-001",
            LocationNodeId = "shelf-a1",
            ZoneCount = 3,
            RenderJobStatus = "Queued",
        };

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<ApplyDeviceAssignmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<ApplyDeviceAssignmentResponse>>(responseData));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/Assignments/Apply")
        {
            Content = JsonContent.Create(new
            {
                deviceId = "disp-001",
                locationNodeId = "shelf-a1",
                zones = new[]
                {
                    new { zoneIndex = 1, productId = "prod-001" },
                    new { zoneIndex = 2, productId = "prod-002" },
                    new { zoneIndex = 3, productId = "prod-003" },
                },
            }),
        };

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PreviewDeviceAssignment_ShouldReturn200_WhenRequestIsValid()
    {
        // Arrange
        var previewResponse = new PreviewDeviceAssignmentResponse
        {
            DeviceId = "disp-001",
            LocationNodeId = "shelf-a1",
            ZoneCount = 1,
        };

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<PreviewDeviceAssignmentQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<PreviewDeviceAssignmentResponse>>(previewResponse));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/Assignments/Preview")
        {
            Content = JsonContent.Create(new
            {
                deviceId = "disp-001",
                locationNodeId = "shelf-a1",
                zones = new[]
                {
                    new { zoneIndex = 1, productId = "prod-001" },
                },
            }),
        };

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

#pragma warning restore CA2012
