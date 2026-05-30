// <copyright file="UpdateAccessPointStatusCommandHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.AccessPoints.Features.UpdateAccessPointStatus.V1;
using Device.Domain.AccessPoints;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Database;
using SharedKernel.Events;
using Shouldly;
using Wolverine;

namespace Device.UnitTests.Application.AccessPoints;

public sealed class UpdateAccessPointStatusCommandHandlerTests
{
    private readonly IAccessPointReadRepository _readRepository = Substitute.For<IAccessPointReadRepository>();
    private readonly IAccessPointWriteRepository _writeRepository = Substitute.For<IAccessPointWriteRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task Handle_WhenAccessPointExists_ShouldUpdateStatusAndPersist()
    {
        // Arrange
        AccessPoint accessPoint = AccessPoint.Create("AP-001", "Hanshow", "zone-a", 10).Value;
        _readRepository.GetBySerialAsync("AP-001", Arg.Any<CancellationToken>()).Returns(accessPoint);

        UpdateAccessPointStatusCommandHandler handler = new(_readRepository, _writeRepository, _unitOfWork, _messageBus);
        UpdateAccessPointStatusCommand command = new(" ap-001 ", AccessPointStatus.Offline);

        // Act
        ErrorOr<UpdateAccessPointStatusResponse> result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.AccessPointId.ShouldBe(accessPoint.Id);
        result.Value.Status.ShouldBe(AccessPointStatus.Offline);

        await _writeRepository.Received(1).UpdateAsync(accessPoint, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _messageBus.Received(1).PublishAsync(Arg.Any<AccessPointStatusChangedIntegrationEvent>());
    }

    [Fact]
    public async Task Handle_WhenAccessPointDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        _readRepository.GetBySerialAsync("AP-404", Arg.Any<CancellationToken>()).Returns((AccessPoint?)null);

        UpdateAccessPointStatusCommandHandler handler = new(_readRepository, _writeRepository, _unitOfWork, _messageBus);
        UpdateAccessPointStatusCommand command = new("ap-404", AccessPointStatus.Online);

        // Act
        ErrorOr<UpdateAccessPointStatusResponse> result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("AccessPoint.NotFound");

        await _writeRepository.DidNotReceive().UpdateAsync(Arg.Any<AccessPoint>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenStatusChanges_ShouldCallRepositorySaveOnce()
    {
        // Arrange
        AccessPoint accessPoint = AccessPoint.Create("AP-002", "Hanshow", "zone-b", 10).Value;
        accessPoint.SetStatus(AccessPointStatus.Online);
        _readRepository.GetBySerialAsync("AP-002", Arg.Any<CancellationToken>()).Returns(accessPoint);

        UpdateAccessPointStatusCommandHandler handler = new(_readRepository, _writeRepository, _unitOfWork, _messageBus);
        UpdateAccessPointStatusCommand command = new("AP-002", AccessPointStatus.Maintenance);

        // Act
        _ = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await _writeRepository.Received(1).UpdateAsync(Arg.Any<AccessPoint>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
