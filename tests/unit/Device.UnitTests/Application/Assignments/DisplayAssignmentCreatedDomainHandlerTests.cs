// <copyright file="DisplayAssignmentCreatedDomainHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;
using Device.Application.Assignments.EventHandlers.DomainEvents;
using Device.Domain.Entities.DisplayAssignmentAggregate;
using Device.Domain.Entities.DisplayAssignmentAggregate.Events;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Wolverine;

namespace Device.UnitTests.Application.Assignments;

public sealed class DisplayAssignmentCreatedDomainHandlerTests
{
    [Fact]
    public async Task Handle_WhenAssignmentExists_ShouldPublishCreatedAndStartOperation()
    {
        IMessageBus messageBus = Substitute.For<IMessageBus>();
        IDisplayAssignmentWriteRepository repository = Substitute.For<IDisplayAssignmentWriteRepository>();
        ILogger<DisplayAssignmentCreatedDomainHandler> logger = Substitute.For<ILogger<DisplayAssignmentCreatedDomainHandler>>();

        Guid assignmentId = Guid.NewGuid();
        Guid displayId = Guid.NewGuid();
        Guid renderJobId = Guid.NewGuid();
        DisplayAssignmentCreatedEvent domainEvent = new(assignmentId, displayId, renderJobId);

        DisplayAssignment assignment = DisplayAssignment.Create(displayId, "zone-a", "template-1", "Request", null, null, [new DisplayAssignmentZone(1, Guid.NewGuid())]).Value;
        repository.FindOneAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DisplayAssignment, bool>>>(), false, Arg.Any<CancellationToken>()).Returns(assignment);

        DisplayAssignmentCreatedDomainHandler handler = new(messageBus, repository, logger);
        await handler.Handle(domainEvent, TestContext.Current.CancellationToken);

        await messageBus.Received(2).PublishAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_WhenAssignmentMissing_ShouldPublishCreatedOnly()
    {
        IMessageBus messageBus = Substitute.For<IMessageBus>();
        IDisplayAssignmentWriteRepository repository = Substitute.For<IDisplayAssignmentWriteRepository>();
        ILogger<DisplayAssignmentCreatedDomainHandler> logger = Substitute.For<ILogger<DisplayAssignmentCreatedDomainHandler>>();

        DisplayAssignmentCreatedEvent domainEvent = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        repository.FindOneAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DisplayAssignment, bool>>>(), false, Arg.Any<CancellationToken>()).Returns((DisplayAssignment?)null);

        DisplayAssignmentCreatedDomainHandler handler = new(messageBus, repository, logger);
        await handler.Handle(domainEvent, TestContext.Current.CancellationToken);

        await messageBus.Received(1).PublishAsync(Arg.Any<object>());
    }
}
