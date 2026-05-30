// <copyright file="GetDeviceDefinitionsQueryHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.Abstractions;
using Device.Application.DeviceDefinitions.Features.GetDeviceDefinitions.V1;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Devices;
using SharedKernel.Core.Pagination;
using Shouldly;

#pragma warning disable CA2012

namespace Device.UnitTests.Application.DeviceDefinitions;

public sealed class GetDeviceDefinitionsQueryHandlerTests
{
    private readonly IDeviceDefinitionReadRepository _repository;
    private readonly GetDeviceDefinitionsQueryHandler _handler;

    public GetDeviceDefinitionsQueryHandlerTests()
    {
        _repository = Substitute.For<IDeviceDefinitionReadRepository>();
        _handler = new GetDeviceDefinitionsQueryHandler(_repository);
    }

    [Fact]
    public async Task Handle_ShouldReturnPagedItems_WhenDefinitionsExist()
    {
        // Arrange
        var idOne = Guid.NewGuid();
        var idTwo = Guid.NewGuid();

        var snapshots = new List<DeviceDefinitionSnapshot>
        {
            new(idOne, "HS-SE2130R", "Hanshow 2.13\" Red", 250, 122, DisplayInkColor.Black, SupportsNfc: true, EslProvider.Hanshow, null, null, null),
            new(idTwo, "SL-P154", "SoluM 1.54\"", null, null, DisplayInkColor.Black, SupportsNfc: false, EslProvider.SoluM, null, null, null),
        };

        _repository
            .GetPagedAsync(1, 10, null, false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedList<DeviceDefinitionSnapshot>(snapshots, totalItems: 2, page: 1, size: 10)));

        // Act
        ErrorOr<PagedList<GetDeviceDefinitionItemResponse>> result = await _handler.Handle(
            new GetDeviceDefinitionsQuery(Page: 1, Size: 10, SortBy: null, SortDescending: false),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Items.Count.ShouldBe(2);
        result.Value.TotalItems.ShouldBe(2);
        result.Value.Items[0].Id.ShouldBe(idOne);
        result.Value.Items[0].ModelId.ShouldBe("HS-SE2130R");
        result.Value.Items[1].Id.ShouldBe(idTwo);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyPage_WhenNoDefinitionsExist()
    {
        // Arrange
        _repository
            .GetPagedAsync(1, 10, null, false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedList<DeviceDefinitionSnapshot>([], totalItems: 0, page: 1, size: 10)));

        // Act
        ErrorOr<PagedList<GetDeviceDefinitionItemResponse>> result = await _handler.Handle(
            new GetDeviceDefinitionsQuery(Page: 1, Size: 10, SortBy: null, SortDescending: false),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Items.ShouldBeEmpty();
        result.Value.TotalItems.ShouldBe(0);
    }
}

#pragma warning restore CA2012
