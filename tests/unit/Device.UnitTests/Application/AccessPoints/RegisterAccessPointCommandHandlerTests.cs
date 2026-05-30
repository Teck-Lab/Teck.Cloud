// <copyright file="RegisterAccessPointCommandHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.AccessPoints.Features.RegisterAccessPoint.V1;
using Device.Domain.AccessPoints;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Database;
using SharedKernel.Events;
using Shouldly;
using Wolverine;

#pragma warning disable CA2012

namespace Device.UnitTests.Application.AccessPoints;

public sealed class RegisterAccessPointCommandHandlerTests
{
    private readonly IAccessPointReadRepository _readRepository;
    private readonly IAccessPointWriteRepository _writeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessageBus _messageBus;
    private readonly RegisterAccessPointCommandHandler _handler;

    public RegisterAccessPointCommandHandlerTests()
    {
        _readRepository = Substitute.For<IAccessPointReadRepository>();
        _writeRepository = Substitute.For<IAccessPointWriteRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _messageBus = Substitute.For<IMessageBus>();
        _handler = new RegisterAccessPointCommandHandler(_readRepository, _writeRepository, _unitOfWork, _messageBus);
    }

    [Fact]
    public async Task Handle_WhenSerialIsUnique_ShouldRegisterPublishAndPersist()
    {
        // Arrange
        _readRepository
            .GetBySerialAsync("AP-001", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccessPoint?>(null));

        var command = new RegisterAccessPointCommand("  ap-001  ", "Hanshow", "shelf-a1", 25);

        // Act
        ErrorOr<RegisterAccessPointResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.AccessPointId.ShouldNotBe(Guid.Empty);

        await _writeRepository.Received(1)
            .AddAsync(Arg.Any<AccessPoint>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1)
            .SaveChangesAsync(Arg.Any<CancellationToken>());
        await _messageBus.Received(1)
            .PublishAsync(Arg.Is<AccessPointRegisteredIntegrationEvent>(x =>
                x.AccessPointId == result.Value.AccessPointId &&
                x.SerialNumber == "AP-001" &&
                x.Vendor == "Hanshow" &&
                x.LocationNodeId == "shelf-a1" &&
                x.MaxCapacity == 25));
    }

    [Fact]
    public async Task Handle_WhenSerialAlreadyExists_ShouldReturnConflictAndNotPersist()
    {
        // Arrange
        AccessPoint existing = AccessPoint.Create("AP-001", "Hanshow", "shelf-a1", 10).Value;
        _readRepository
            .GetBySerialAsync("AP-001", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccessPoint?>(existing));

        var command = new RegisterAccessPointCommand("AP-001", "Hanshow", "shelf-a1", 25);

        // Act
        ErrorOr<RegisterAccessPointResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe("AccessPoint.DuplicateSerialNumber");

        await _writeRepository.DidNotReceive()
            .AddAsync(Arg.Any<AccessPoint>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive()
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDomainValidationFails_ShouldReturnValidationErrorAndNotPersist()
    {
        // Arrange
        _readRepository
            .GetBySerialAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccessPoint?>(null));

        var command = new RegisterAccessPointCommand(string.Empty, "Hanshow", "shelf-a1", 25);

        // Act
        ErrorOr<RegisterAccessPointResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Type == ErrorType.Validation);

        await _writeRepository.DidNotReceive()
            .AddAsync(Arg.Any<AccessPoint>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive()
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

#pragma warning restore CA2012
