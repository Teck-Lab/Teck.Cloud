// <copyright file="DisplayAssignmentFailedDomainHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.EventHandlers.DomainEvents;
using Device.Domain.Entities.DisplayAssignmentAggregate.Events;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Wolverine;

namespace Device.UnitTests.Application.Assignments;

public sealed class DisplayAssignmentFailedDomainHandlerTests
{
    [Fact]
    public async Task Handle_WhenInvoked_ShouldPublishFailedIntegrationEvent()
    {
        IMessageBus messageBus = Substitute.For<IMessageBus>();
        ILogger<DisplayAssignmentFailedDomainHandler> logger = Substitute.For<ILogger<DisplayAssignmentFailedDomainHandler>>();
        DisplayAssignmentFailedDomainHandler handler = new(messageBus, logger);

        await handler.Handle(new DisplayAssignmentFailedEvent(Guid.NewGuid(), Guid.NewGuid(), "render failed"));

        await messageBus.Received(1).PublishAsync(Arg.Any<object>());
    }
}
