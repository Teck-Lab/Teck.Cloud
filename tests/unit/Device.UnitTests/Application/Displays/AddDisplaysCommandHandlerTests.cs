// <copyright file="AddDisplaysCommandHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Displays.Abstractions;
using Device.Application.Displays.Features.AddDisplays.V1;
using Device.Domain.Entities.DisplayAggregate;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Database;
using Shouldly;

#pragma warning disable CA2012

namespace Device.UnitTests.Application.Displays;

public sealed class AddDisplaysCommandHandlerTests
{
    private readonly IDisplayWriteRepository _displayWriteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AddDisplaysCommandHandler _handler;

    public AddDisplaysCommandHandlerTests()
    {
        _displayWriteRepository = Substitute.For<IDisplayWriteRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new AddDisplaysCommandHandler(_displayWriteRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ShouldAddAllDisplays_WhenNoSerialsAreDuplicates()
    {
        // Arrange
        _displayWriteRepository
            .ExistsWithShortSerialGlobalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _displayWriteRepository
            .AddAsync(Arg.Any<Display>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _unitOfWork
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(2));

        var command = new AddDisplaysCommand(
            LocationNodeId: "shelf-a1",
            Serials: ["AE-6F-B8-87", "00-11-22-33"],
            DeviceDefinitionId: null);

        // Act
        ErrorOr<AddDisplaysResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.AddedCount.ShouldBe(2);
        result.Value.DuplicateCount.ShouldBe(0);
        result.Value.Results.Count.ShouldBe(2);
        result.Value.Results.ShouldAllBe(r => !r.Duplicate);
        result.Value.Results.ShouldAllBe(r => r.DisplayId.HasValue);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCountDuplicates_WhenSomeSerialAlreadyRegistered()
    {
        // Arrange — first serial is new, second is a duplicate
        _displayWriteRepository
            .ExistsWithShortSerialGlobalAsync("AE-6F-B8-87", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _displayWriteRepository
            .ExistsWithShortSerialGlobalAsync("00-11-22-33", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        _displayWriteRepository
            .AddAsync(Arg.Any<Display>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _unitOfWork
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        var command = new AddDisplaysCommand(
            LocationNodeId: "shelf-a1",
            Serials: ["AE-6F-B8-87", "00-11-22-33"],
            DeviceDefinitionId: null);

        // Act
        ErrorOr<AddDisplaysResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.AddedCount.ShouldBe(1);
        result.Value.DuplicateCount.ShouldBe(1);
        result.Value.Results.Count.ShouldBe(2);

        AddDisplayResult newResult = result.Value.Results.Single(r => r.ShortSerial == "AE-6F-B8-87");
        newResult.Duplicate.ShouldBeFalse();
        newResult.DisplayId.ShouldNotBeNull();

        AddDisplayResult dupResult = result.Value.Results.Single(r => r.ShortSerial == "00-11-22-33");
        dupResult.Duplicate.ShouldBeTrue();
        dupResult.DisplayId.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnAllDuplicates_WhenAllSerialsAlreadyRegistered()
    {
        // Arrange
        _displayWriteRepository
            .ExistsWithShortSerialGlobalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var command = new AddDisplaysCommand(
            LocationNodeId: "shelf-a1",
            Serials: ["AE-6F-B8-87", "00-11-22-33"],
            DeviceDefinitionId: null);

        // Act
        ErrorOr<AddDisplaysResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.AddedCount.ShouldBe(0);
        result.Value.DuplicateCount.ShouldBe(2);
        result.Value.Results.ShouldAllBe(r => r.Duplicate);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotCallSaveChanges_WhenSerialListIsEmpty()
    {
        // Arrange
        var command = new AddDisplaysCommand(
            LocationNodeId: "shelf-a1",
            Serials: [],
            DeviceDefinitionId: null);

        // Act
        ErrorOr<AddDisplaysResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.AddedCount.ShouldBe(0);
        result.Value.DuplicateCount.ShouldBe(0);
        result.Value.Results.ShouldBeEmpty();

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNormaliseSerial_BeforeCheckingDuplicates()
    {
        // Arrange — input is lowercase with extra whitespace
        _displayWriteRepository
            .ExistsWithShortSerialGlobalAsync("AE-6F-B8-87", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _displayWriteRepository
            .AddAsync(Arg.Any<Display>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _unitOfWork
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        var command = new AddDisplaysCommand(
            LocationNodeId: "shelf-a1",
            Serials: ["  ae-6f-b8-87  "],
            DeviceDefinitionId: null);

        // Act
        ErrorOr<AddDisplaysResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.AddedCount.ShouldBe(1);
        result.Value.Results[0].ShortSerial.ShouldBe("AE-6F-B8-87");

        await _displayWriteRepository.Received(1)
            .ExistsWithShortSerialGlobalAsync("AE-6F-B8-87", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAssignDeviceDefinitionId_WhenProvided()
    {
        // Arrange
        var definitionId = Guid.NewGuid();

        Display? capturedDisplay = null;

        _displayWriteRepository
            .ExistsWithShortSerialGlobalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _displayWriteRepository
            .AddAsync(Arg.Do<Display>(d => capturedDisplay = d), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _unitOfWork
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        var command = new AddDisplaysCommand(
            LocationNodeId: "shelf-a1",
            Serials: ["AE-6F-B8-87"],
            DeviceDefinitionId: definitionId);

        // Act
        ErrorOr<AddDisplaysResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        capturedDisplay.ShouldNotBeNull();
        capturedDisplay!.DeviceDefinitionId.ShouldBe(definitionId);
    }
}

#pragma warning restore CA2012
