// <copyright file="ApplyDeviceAssignmentEndpointTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using Device.Application.Assignments.Features.ApplyDeviceAssignment.V1;
using Device.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;

#pragma warning disable CA2012

namespace Device.IntegrationTests.Endpoints.Assignments;

public sealed class ApplyDeviceAssignmentEndpointTests
{
    [Fact]
    public async Task ApplyDeviceAssignment_ShouldReturn200_WhenCommandSucceeds()
    {
        // Arrange
        var displayId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var response = new ApplyDeviceAssignmentResponse
        {
            DeviceId = displayId.ToString(),
            LocationNodeId = "shelf-a1",
            ResolvedTemplateId = "template-default",
            TemplateSource = "Ancestor",
            ZoneCount = 1,
            DuplicateProductsAllowed = true,
            RenderJobId = jobId,
            RenderJobStatus = "queued",
        };

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<ApplyDeviceAssignmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<ApplyDeviceAssignmentResponse>>(response));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/Assignments/Apply")
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

        ApplyDeviceAssignmentResponse? body = await httpResponse.Content
            .ReadFromJsonAsync<ApplyDeviceAssignmentResponse>(cancellationToken: TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body!.RenderJobId.ShouldBe(jobId);
        body.RenderJobStatus.ShouldBe("queued");
    }

    [Fact]
    public async Task ApplyDeviceAssignment_ShouldReturn422_WhenDeviceIdIsInvalidGuid()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<ApplyDeviceAssignmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<ApplyDeviceAssignmentResponse>>(
                Error.Validation("Device.InvalidDeviceIdFormat", "Device ID is not a valid GUID.")));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/Assignments/Apply")
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
    public async Task ApplyDeviceAssignment_ShouldReturn404_WhenLayoutContextNotFound()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<ApplyDeviceAssignmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<ApplyDeviceAssignmentResponse>>(
                Error.NotFound("Device.LayoutNotFound", "Layout not found for this display.")));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/Assignments/Apply")
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
}

#pragma warning restore CA2012
