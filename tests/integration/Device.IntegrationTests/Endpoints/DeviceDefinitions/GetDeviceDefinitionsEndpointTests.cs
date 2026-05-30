// <copyright file="GetDeviceDefinitionsEndpointTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using Device.Application.DeviceDefinitions.Features.GetDeviceDefinitions.V1;
using Device.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using SharedKernel.Core.Pagination;
using Shouldly;
#pragma warning disable CA2012

namespace Device.IntegrationTests.Endpoints.DeviceDefinitions;

public sealed class GetDeviceDefinitionsEndpointTests
{
    [Fact]
    public async Task GetDeviceDefinitions_ShouldReturn200_WithPagedItems_WhenDefinitionsExist()
    {
        // Arrange
        Guid idOne = Guid.NewGuid();
        Guid idTwo = Guid.NewGuid();
        GetDeviceDefinitionsQuery? capturedQuery = null;

        PagedList<GetDeviceDefinitionItemResponse> pagedResponse = new(
            [
                new(idOne, "HS-SE2130R", "Hanshow 2.13\" Red", 250, 122, 1, false, "Hanshow", null, null, null),
                new(idTwo, "SL-P154", "SoluM 1.54\"", null, null, 1, false, "SoluM", null, null, null),
            ],
            totalItems: 2,
            page: 1,
            size: 20);

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetDeviceDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedQuery = callInfo.Arg<GetDeviceDefinitionsQuery>();
                return new ValueTask<ErrorOr<PagedList<GetDeviceDefinitionItemResponse>>>(pagedResponse);
            });

        await using TestDeviceAdminApiHost host = await TestDeviceAdminApiHost.StartAsync(sender);

        // Act
        HttpResponseMessage response = await host.Client.GetAsync(
            new Uri("/device/v1/DeviceDefinitions?page=1&size=20", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedQuery.ShouldNotBeNull();
        capturedQuery.Page.ShouldBe(1);
        capturedQuery.Size.ShouldBe(20);

        PagedList<GetDeviceDefinitionItemResponse>? body = await response.Content
            .ReadFromJsonAsync<PagedList<GetDeviceDefinitionItemResponse>>(cancellationToken: TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Items.Count.ShouldBe(2);
        body.TotalItems.ShouldBe(2);
    }
}
