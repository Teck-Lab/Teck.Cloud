// <copyright file="GetDeviceLayoutsQueryHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceLayouts.Abstractions;
using Device.Application.DeviceLayouts.Features.GetDeviceLayouts.V1;
using ErrorOr;
using NSubstitute;
using Shouldly;

#pragma warning disable CA2012

namespace Device.UnitTests.Application.DeviceLayouts;

public sealed class GetDeviceLayoutsQueryHandlerTests
{
    private readonly IDeviceLayoutReadRepository _repository;
    private readonly GetDeviceLayoutsQueryHandler _handler;

    public GetDeviceLayoutsQueryHandlerTests()
    {
        _repository = Substitute.For<IDeviceLayoutReadRepository>();
        _handler = new GetDeviceLayoutsQueryHandler(_repository);
    }

    [Fact]
    public async Task Handle_ShouldReturnLayouts_WhenLayoutsExistForDefinition()
    {
        // Arrange
        var definitionId = Guid.NewGuid();
        var layoutIdOne = Guid.NewGuid();
        var layoutIdTwo = Guid.NewGuid();

        IReadOnlyList<DeviceLayoutSnapshot> snapshots =
        [
            new DeviceLayoutSnapshot(layoutIdOne, definitionId, "3-zone standard", 3),
            new DeviceLayoutSnapshot(layoutIdTwo, definitionId, "Single-zone", 1),
        ];

        _repository
            .GetByDeviceDefinitionIdAsync(definitionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(snapshots));

        // Act
        ErrorOr<IReadOnlyList<GetDeviceLayoutItemResponse>> result = await _handler.Handle(
            new GetDeviceLayoutsQuery(definitionId),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Count.ShouldBe(2);
        result.Value[0].Id.ShouldBe(layoutIdOne);
        result.Value[0].Name.ShouldBe("3-zone standard");
        result.Value[0].MaxZoneCount.ShouldBe(3);
        result.Value[1].Id.ShouldBe(layoutIdTwo);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoLayoutsExist()
    {
        // Arrange
        var definitionId = Guid.NewGuid();

        _repository
            .GetByDeviceDefinitionIdAsync(definitionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DeviceLayoutSnapshot>>([]));

        // Act
        ErrorOr<IReadOnlyList<GetDeviceLayoutItemResponse>> result = await _handler.Handle(
            new GetDeviceLayoutsQuery(definitionId),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBeEmpty();
    }
}

#pragma warning restore CA2012
