// <copyright file="GetAccessPointsQueryHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.AccessPoints.Features.GetAccessPoints.V1;
using Device.Domain.AccessPoints;
using ErrorOr;
using NSubstitute;
using Shouldly;

#pragma warning disable CA2012

namespace Device.UnitTests.Application.AccessPoints;

public sealed class GetAccessPointsQueryHandlerTests
{
    private readonly IAccessPointReadRepository _repository;
    private readonly GetAccessPointsQueryHandler _handler;

    public GetAccessPointsQueryHandlerTests()
    {
        _repository = Substitute.For<IAccessPointReadRepository>();
        _handler = new GetAccessPointsQueryHandler(_repository);
    }

    [Fact]
    public async Task Handle_WhenLocationHasAccessPoints_ShouldReturnMappedItems()
    {
        // Arrange
        const string locationNodeId = "shelf-a1";

        AccessPoint first = AccessPoint.Create("AP-001", "Hanshow", locationNodeId, 10).Value;
        first.IncrementLoad();

        AccessPoint second = AccessPoint.Create("AP-002", "Hanshow", locationNodeId, 20).Value;
        second.SetStatus(AccessPointStatus.Offline);

        IReadOnlyList<AccessPoint> accessPoints = [first, second];

        _repository
            .GetByLocationAsync(locationNodeId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(accessPoints));

        var query = new GetAccessPointsQuery(locationNodeId);

        // Act
        ErrorOr<IReadOnlyList<GetAccessPointItemResponse>> result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Count.ShouldBe(2);

        result.Value[0].AccessPointId.ShouldBe(first.Id);
        result.Value[0].SerialNumber.ShouldBe("AP-001");
        result.Value[0].LocationNodeId.ShouldBe(locationNodeId);
        result.Value[0].Status.ShouldBe(AccessPointStatus.Online);
        result.Value[0].CurrentLoad.ShouldBe(1);
        result.Value[0].MaxCapacity.ShouldBe(10);

        result.Value[1].AccessPointId.ShouldBe(second.Id);
        result.Value[1].SerialNumber.ShouldBe("AP-002");
        result.Value[1].Status.ShouldBe(AccessPointStatus.Offline);
        result.Value[1].CurrentLoad.ShouldBe(0);
        result.Value[1].MaxCapacity.ShouldBe(20);
    }

    [Fact]
    public async Task Handle_WhenNoAccessPointsExist_ShouldReturnEmptyList()
    {
        // Arrange
        _repository
            .GetByLocationAsync("empty-node", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AccessPoint>>([]));

        var query = new GetAccessPointsQuery("empty-node");

        // Act
        ErrorOr<IReadOnlyList<GetAccessPointItemResponse>> result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBeEmpty();
    }
}

#pragma warning restore CA2012
