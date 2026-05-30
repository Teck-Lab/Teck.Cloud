// <copyright file="GetDevicesEndpointTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using Device.Application.Devices.Features.GetDevices.V1;
using Device.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using SharedKernel.Core.Pagination;
using Shouldly;

#pragma warning disable CA2012

namespace Device.IntegrationTests.Endpoints.Devices;

public sealed class GetDevicesEndpointTests
{
    [Fact]
    public async Task GetDevices_ShouldReturn200_WithPagedList_WhenQuerySucceeds()
    {
        // Arrange
        var idOne = Guid.NewGuid();
        var idTwo = Guid.NewGuid();

        var items = new List<GetDeviceItemResponse>
        {
            new(idOne, "HS-MODEL-A", "Hanshow Model A", "Hanshow"),
            new(idTwo, "SM-MODEL-B", "SoluM Model B", "SoluM"),
        };

        var pagedList = new PagedList<GetDeviceItemResponse>(items, 2, 1, 10);

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetDevicesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<PagedList<GetDeviceItemResponse>>>(pagedList));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        // Act
        HttpResponseMessage response = await host.Client.GetAsync(
            "/device/v1/Devices?page=1&size=10",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        PagedList<GetDeviceItemResponse>? body = await response.Content
            .ReadFromJsonAsync<PagedList<GetDeviceItemResponse>>(cancellationToken: TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body!.TotalItems.ShouldBe(2);
        body.Items.Count.ShouldBe(2);
        body.Items[0].ModelId.ShouldBe("HS-MODEL-A");
        body.Items[1].ModelId.ShouldBe("SM-MODEL-B");
    }

    [Fact]
    public async Task GetDevices_ShouldPassSortParameters_ToQuery()
    {
        // Arrange
        var pagedList = new PagedList<GetDeviceItemResponse>([], 0, 1, 5);

        GetDevicesQuery? capturedQuery = null;

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Do<GetDevicesQuery>(q => capturedQuery = q), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<PagedList<GetDeviceItemResponse>>>(pagedList));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        // Act
        await host.Client.GetAsync(
            "/device/v1/Devices?page=2&size=5&sortBy=name&sortDescending=true",
            TestContext.Current.CancellationToken);

        // Assert
        capturedQuery.ShouldNotBeNull();
        capturedQuery!.Page.ShouldBe(2);
        capturedQuery.Size.ShouldBe(5);
        capturedQuery.SortBy.ShouldBe("name");
        capturedQuery.SortDescending.ShouldBeTrue();
    }

    [Fact]
    public async Task GetDevices_ShouldReturn200_WithEmptyList_WhenNoDevicesExist()
    {
        // Arrange
        var pagedList = new PagedList<GetDeviceItemResponse>([], 0, 1, 10);

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetDevicesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<PagedList<GetDeviceItemResponse>>>(pagedList));

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        // Act
        HttpResponseMessage response = await host.Client.GetAsync(
            "/device/v1/Devices",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        PagedList<GetDeviceItemResponse>? body = await response.Content
            .ReadFromJsonAsync<PagedList<GetDeviceItemResponse>>(cancellationToken: TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body!.TotalItems.ShouldBe(0);
        body.Items.ShouldBeEmpty();
    }
}

#pragma warning restore CA2012
