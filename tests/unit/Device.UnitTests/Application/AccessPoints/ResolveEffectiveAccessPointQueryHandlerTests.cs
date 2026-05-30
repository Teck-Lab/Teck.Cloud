// <copyright file="ResolveEffectiveAccessPointQueryHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.AccessPoints.Features.ResolveEffectiveAccessPoint.V1;
using Device.Domain.AccessPoints;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Database;
using SharedKernel.Events;
using Shouldly;
using Wolverine;

#pragma warning disable CA2012

namespace Device.UnitTests.Application.AccessPoints;

public sealed class ResolveEffectiveAccessPointQueryHandlerTests
{
    private readonly IAccessPointReadRepository _readRepository;
    private readonly IAccessPointWriteRepository _writeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessageBus _messageBus;
    private readonly ResolveEffectiveAccessPointQueryHandler _handler;

    public ResolveEffectiveAccessPointQueryHandlerTests()
    {
        _readRepository = Substitute.For<IAccessPointReadRepository>();
        _writeRepository = Substitute.For<IAccessPointWriteRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _messageBus = Substitute.For<IMessageBus>();
        _handler = new ResolveEffectiveAccessPointQueryHandler(_readRepository, _writeRepository, _unitOfWork, _messageBus);
    }

    [Fact]
    public async Task Handle_WhenAccessPointExistsInOwnLocation_ShouldReturnAccessPoint()
    {
        // Arrange
        AccessPoint own = AccessPoint.Create("AP-001", "Hanshow", "loc-own", 2).Value;
        _readRepository
            .FindByVendorAndLocationAsync("Hanshow", "loc-own", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccessPoint?>(own));

        var query = new ResolveEffectiveAccessPointQuery("loc-own", "Hanshow", []);

        // Act
        ErrorOr<ResolveEffectiveAccessPointResponse> result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.AccessPointId.ShouldBe(own.Id);
        result.Value.LocationNodeId.ShouldBe("loc-own");
        result.Value.CurrentLoad.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_WhenOwnLocationHasNoAccessPoint_ShouldFindAccessPointInParentLocation()
    {
        // Arrange
        AccessPoint parent = AccessPoint.Create("AP-002", "Hanshow", "loc-parent", 3).Value;

        _readRepository
            .FindByVendorAndLocationAsync("Hanshow", "loc-own", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccessPoint?>(null));
        _readRepository
            .FindByVendorAndLocationAsync("Hanshow", "loc-parent", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccessPoint?>(parent));

        var query = new ResolveEffectiveAccessPointQuery("loc-own", "Hanshow", ["loc-parent"]);

        // Act
        ErrorOr<ResolveEffectiveAccessPointResponse> result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.AccessPointId.ShouldBe(parent.Id);
        result.Value.LocationNodeId.ShouldBe("loc-parent");
    }

    [Fact]
    public async Task Handle_WhenNoAccessPointAvailableInChain_ShouldReturnNotFound()
    {
        // Arrange
        _readRepository
            .FindByVendorAndLocationAsync("Hanshow", "loc-own", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccessPoint?>(null));
        _readRepository
            .FindByVendorAndLocationAsync("Hanshow", "loc-parent", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccessPoint?>(null));

        var query = new ResolveEffectiveAccessPointQuery("loc-own", "Hanshow", ["loc-parent"]);

        // Act
        ErrorOr<ResolveEffectiveAccessPointResponse> result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("AccessPoint.EffectiveAccessPointNotFound");
    }

    [Fact]
    public async Task Handle_WhenResolvedSuccessfully_ShouldIncrementLoadAndPublishLoadChangedIntegrationEvent()
    {
        // Arrange
        AccessPoint own = AccessPoint.Create("AP-001", "Hanshow", "loc-own", 2).Value;
        _readRepository
            .FindByVendorAndLocationAsync("Hanshow", "loc-own", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccessPoint?>(own));

        var query = new ResolveEffectiveAccessPointQuery("loc-own", "Hanshow", []);

        // Act
        ErrorOr<ResolveEffectiveAccessPointResponse> result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        own.CurrentLoad.ShouldBe(1);

        await _writeRepository.Received(1)
            .UpdateAsync(own, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1)
            .SaveChangesAsync(Arg.Any<CancellationToken>());
        await _messageBus.Received(1)
            .PublishAsync(Arg.Is<AccessPointLoadChangedIntegrationEvent>(x =>
                x.AccessPointId == own.Id &&
                x.SerialNumber == own.SerialNumber &&
                x.LocationNodeId == own.LocationNodeId &&
                x.PreviousLoad == 0 &&
                x.NewLoad == 1 &&
                x.MaxCapacity == own.MaxCapacity));
    }
}

#pragma warning restore CA2012
