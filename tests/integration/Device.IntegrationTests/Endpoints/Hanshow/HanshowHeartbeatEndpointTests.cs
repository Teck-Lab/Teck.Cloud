// <copyright file="HanshowHeartbeatEndpointTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using Device.Application.Hanshow.Features.Heartbeat.V1;
using Device.IntegrationTests.TestSupport;
using Mediator;
using NSubstitute;
using Shouldly;

namespace Device.IntegrationTests.Endpoints.Hanshow;

public sealed class HanshowHeartbeatEndpointTests
{
    [Fact]
    public async Task Heartbeat_ShouldReturn204_WhenPayloadIsValid()
    {
        ISender sender = Substitute.For<ISender>();
        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/Hanshow/Heartbeat")
        {
            Content = JsonContent.Create(new HanshowHeartbeatRequest { ShortSerial = "AE-6F-B8-87", LongSerial = 229582052926557319L }),
        };

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Heartbeat_ShouldReturn204_WhenPayloadValuesAreEmpty()
    {
        ISender sender = Substitute.For<ISender>();
        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/device/v1/Hanshow/Heartbeat")
        {
            Content = JsonContent.Create(new HanshowHeartbeatRequest()),
        };

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
