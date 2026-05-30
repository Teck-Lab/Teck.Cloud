// <copyright file="GetDevicesQueryHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.Abstractions;
using Device.Application.Devices.Features.GetDevices.V1;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Devices;
using SharedKernel.Core.Pagination;
using Shouldly;

#pragma warning disable CA2012

namespace Device.UnitTests.Application.Devices;

public sealed class GetDevicesQueryHandlerTests
{
    private readonly IDeviceDefinitionReadRepository _repository;
    private readonly GetDevicesQueryHandler _handler;

    public GetDevicesQueryHandlerTests()
    {
        _repository = Substitute.For<IDeviceDefinitionReadRepository>();
        _handler = new GetDevicesQueryHandler(_repository);
    }

    [Fact]
    public async Task Handle_ShouldReturnPagedList_WithMappedResponses()
    {
        // Arrange
        var idOne = Guid.NewGuid();
        var idTwo = Guid.NewGuid();

        var snapshots = new List<DeviceDefinitionSnapshot>
        {
            new(idOne, "HS-MODEL-A", "Hanshow Model A", null, null, DisplayInkColor.Black, false, EslProvider.Hanshow, null, null, null),
            new(idTwo, "SM-MODEL-B", "SoluM Model B", 250, 122, DisplayInkColor.Black | DisplayInkColor.Red, true, EslProvider.SoluM, null, null, null),
        };

        _repository
            .GetPagedAsync(1, 10, null, false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedList<DeviceDefinitionSnapshot>(snapshots, 2, 1, 10)));

        var query = new GetDevicesQuery(Page: 1, Size: 10, SortBy: null, SortDescending: false);

        // Act
        ErrorOr<PagedList<GetDeviceItemResponse>> result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.TotalItems.ShouldBe(2);
        result.Value.Items.Count.ShouldBe(2);

        GetDeviceItemResponse first = result.Value.Items[0];
        first.Id.ShouldBe(idOne);
        first.ModelId.ShouldBe("HS-MODEL-A");
        first.Name.ShouldBe("Hanshow Model A");
        first.EslProvider.ShouldBe("Hanshow");

        GetDeviceItemResponse second = result.Value.Items[1];
        second.Id.ShouldBe(idTwo);
        second.EslProvider.ShouldBe("SoluM");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyPagedList_WhenNoDefinitionsExist()
    {
        // Arrange
        _repository
            .GetPagedAsync(1, 10, null, false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedList<DeviceDefinitionSnapshot>([], 0, 1, 10)));

        var query = new GetDevicesQuery(Page: 1, Size: 10, SortBy: null, SortDescending: false);

        // Act
        ErrorOr<PagedList<GetDeviceItemResponse>> result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.TotalItems.ShouldBe(0);
        result.Value.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldPassSortParameters_ToRepository()
    {
        // Arrange
        _repository
            .GetPagedAsync(2, 5, "name", true, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedList<DeviceDefinitionSnapshot>([], 0, 2, 5)));

        var query = new GetDevicesQuery(Page: 2, Size: 5, SortBy: "name", SortDescending: true);

        // Act
        ErrorOr<PagedList<GetDeviceItemResponse>> result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();

        await _repository.Received(1)
            .GetPagedAsync(2, 5, "name", true, Arg.Any<CancellationToken>());
    }
}

#pragma warning restore CA2012
