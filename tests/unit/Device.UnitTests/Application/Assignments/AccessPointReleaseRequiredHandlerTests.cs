// <copyright file="AccessPointReleaseRequiredHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.EventHandlers.IntegrationEvents;
using Device.Domain.AccessPoints;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Core.Database;
using SharedKernel.Events;
using Wolverine;

namespace Device.UnitTests.Application.Assignments;

public sealed class AccessPointReleaseRequiredHandlerTests
{
    [Fact]
    public async Task Handle_WhenAccessPointExists_ShouldDecrementLoadAndPersist()
    {
        IAccessPointReadRepository readRepository = Substitute.For<IAccessPointReadRepository>();
        IAccessPointWriteRepository writeRepository = Substitute.For<IAccessPointWriteRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IMessageBus messageBus = Substitute.For<IMessageBus>();
        ILogger<AccessPointReleaseRequiredHandler> logger = Substitute.For<ILogger<AccessPointReleaseRequiredHandler>>();

        AccessPoint accessPoint = AccessPoint.Create("AP-001", "Hanshow", "zone-a", 5).Value;
        accessPoint.IncrementLoad();
        readRepository.GetBySerialAsync("AP-001", Arg.Any<CancellationToken>()).Returns(accessPoint);

        AccessPointReleaseRequiredHandler handler = new(readRepository, writeRepository, unitOfWork, messageBus, logger);
        await handler.Handle(new AccessPointReleaseRequiredIntegrationEvent(Guid.NewGuid(), "AP-001", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        await writeRepository.Received(1).UpdateAsync(accessPoint, Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await messageBus.Received(1).PublishAsync(Arg.Any<AccessPointLoadChangedIntegrationEvent>());
    }

    [Fact]
    public async Task Handle_WhenAccessPointNotFound_ShouldReturnWithoutPersisting()
    {
        IAccessPointReadRepository readRepository = Substitute.For<IAccessPointReadRepository>();
        IAccessPointWriteRepository writeRepository = Substitute.For<IAccessPointWriteRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IMessageBus messageBus = Substitute.For<IMessageBus>();
        ILogger<AccessPointReleaseRequiredHandler> logger = Substitute.For<ILogger<AccessPointReleaseRequiredHandler>>();

        readRepository.GetBySerialAsync("AP-404", Arg.Any<CancellationToken>()).Returns((AccessPoint?)null);

        AccessPointReleaseRequiredHandler handler = new(readRepository, writeRepository, unitOfWork, messageBus, logger);
        await handler.Handle(new AccessPointReleaseRequiredIntegrationEvent(Guid.NewGuid(), "AP-404", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        await writeRepository.DidNotReceive().UpdateAsync(Arg.Any<AccessPoint>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
