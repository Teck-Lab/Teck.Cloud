// <copyright file="GetDisplaysQueryHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Displays.Abstractions;
using Device.Application.Displays.Features.GetDisplays.V1;
using ErrorOr;
using NSubstitute;
using Shouldly;

#pragma warning disable CA2012

namespace Device.UnitTests.Application.Displays;

public sealed class GetDisplaysQueryHandlerTests
{
    private readonly IDisplayReadRepository _repository;
    private readonly GetDisplaysQueryHandler _handler;

    public GetDisplaysQueryHandlerTests()
    {
        _repository = Substitute.For<IDisplayReadRepository>();
        _handler = new GetDisplaysQueryHandler(_repository);
    }

    [Fact]
    public async Task Handle_ShouldReturnMappedDisplayItems_WhenDisplaysExistForLocation()
    {
        // Arrange
        const string locationNodeId = "shelf-a1";

        var displayIdOne = Guid.NewGuid();
        var displayIdTwo = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        IReadOnlyList<DisplaySnapshot> snapshots = new List<DisplaySnapshot>
        {
            new(displayIdOne, "AE-6F-B8-87", 229582052926557319L, locationNodeId, definitionId, now),
            new(displayIdTwo, "00-11-22-33", null, locationNodeId, null, now.AddMinutes(-5)),
        };

        _repository
            .GetByLocationAsync(locationNodeId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(snapshots));

        var query = new GetDisplaysQuery(locationNodeId);

        // Act
        ErrorOr<IReadOnlyList<GetDisplayItemResponse>> result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Count.ShouldBe(2);

        GetDisplayItemResponse first = result.Value[0];
        first.DisplayId.ShouldBe(displayIdOne);
        first.ShortSerial.ShouldBe("AE-6F-B8-87");
        first.LongSerial.ShouldBe(229582052926557319L);
        first.LocationNodeId.ShouldBe(locationNodeId);
        first.DeviceDefinitionId.ShouldBe(definitionId);
        first.CreatedAt.ShouldBe(now);

        GetDisplayItemResponse second = result.Value[1];
        second.DisplayId.ShouldBe(displayIdTwo);
        second.ShortSerial.ShouldBe("00-11-22-33");
        second.LongSerial.ShouldBeNull();
        second.DeviceDefinitionId.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoDisplaysExistForLocation()
    {
        // Arrange
        IReadOnlyList<DisplaySnapshot> snapshots = new List<DisplaySnapshot>();

        _repository
            .GetByLocationAsync("empty-node", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(snapshots));

        var query = new GetDisplaysQuery("empty-node");

        // Act
        ErrorOr<IReadOnlyList<GetDisplayItemResponse>> result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldPassLocationNodeId_ToRepository()
    {
        // Arrange
        const string locationNodeId = "zone-b2";

        IReadOnlyList<DisplaySnapshot> snapshots = new List<DisplaySnapshot>();

        _repository
            .GetByLocationAsync(locationNodeId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(snapshots));

        var query = new GetDisplaysQuery(locationNodeId);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        await _repository.Received(1)
            .GetByLocationAsync(locationNodeId, Arg.Any<CancellationToken>());
    }
}

#pragma warning restore CA2012
