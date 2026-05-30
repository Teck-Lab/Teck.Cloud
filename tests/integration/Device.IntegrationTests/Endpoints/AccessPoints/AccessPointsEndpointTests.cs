// <copyright file="AccessPointsEndpointTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using Device.Application.AccessPoints.Features.GetAccessPoints.V1;
using Device.Application.AccessPoints.Features.UpdateAccessPointStatus.V1;
using Device.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;

namespace Device.IntegrationTests.Endpoints.AccessPoints;

public sealed class AccessPointsEndpointTests
{
    [Fact]
    public async Task GetAccessPoints_ShouldReturn200_WhenAuthorizedAndValidTenantHeader()
    {
        ISender sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetAccessPointsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<IReadOnlyList<GetAccessPointItemResponse>>>(ErrorOrFactory.From<IReadOnlyList<GetAccessPointItemResponse>>([])));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);
        using HttpRequestMessage request = new(HttpMethod.Get, "/device/v1/AccessPoints?locationNodeId=shelf-a1");
        request.WithAuthenticatedUser().WithScopes("access-point:list");
        request.Headers.Add("X-Tenant-Id", "tecklab");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegisterAccessPoint_ShouldReturn200_WhenAuthorized()
    {
        ISender sender = Substitute.For<ISender>();
        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/AccessPoints/Register")
        {
            Content = JsonContent.Create(new { serialNumber = "", vendor = "", locationNodeId = "", maxCapacity = 0 }),
        };
        request.WithAuthenticatedUser().WithScopes("access-point:create");
        request.Headers.Add("X-Tenant-Id", "tecklab");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateAccessPointStatus_ShouldReturn404_WhenHandlerReturnsNotFound()
    {
        ISender sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<UpdateAccessPointStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<UpdateAccessPointStatusResponse>>(Error.NotFound("AccessPoint.NotFound", "not found")));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);
        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/AccessPoints/AP-404/Status")
        {
            Content = JsonContent.Create(new UpdateAccessPointStatusRequest { Serial = "AP-404", Status = "Online" }),
        };
        request.WithAuthenticatedUser().WithScopes("access-point:update");
        request.Headers.Add("X-Tenant-Id", "tecklab");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
