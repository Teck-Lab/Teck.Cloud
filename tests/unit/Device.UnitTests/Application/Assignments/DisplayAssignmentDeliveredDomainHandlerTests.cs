// <copyright file="DisplayAssignmentDeliveredDomainHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.EventHandlers.DomainEvents;
using Device.Domain.Entities.DisplayAssignmentAggregate.Events;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Wolverine;

namespace Device.UnitTests.Application.Assignments;

public sealed class DisplayAssignmentDeliveredDomainHandlerTests
{
    [Fact]
    public async Task Handle_WhenInvoked_ShouldPublishDeliveredIntegrationEvent()
    {
        IMessageBus messageBus = Substitute.For<IMessageBus>();
        ILogger<DisplayAssignmentDeliveredDomainHandler> logger = Substitute.For<ILogger<DisplayAssignmentDeliveredDomainHandler>>();
        DisplayAssignmentDeliveredDomainHandler handler = new(messageBus, logger);

        await handler.Handle(new DisplayAssignmentDeliveredEvent(Guid.NewGuid(), Guid.NewGuid()));

        await messageBus.Received(1).PublishAsync(Arg.Any<object>());
    }
}
