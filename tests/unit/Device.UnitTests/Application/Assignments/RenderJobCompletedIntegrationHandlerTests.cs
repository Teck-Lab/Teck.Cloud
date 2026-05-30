// <copyright file="RenderJobCompletedIntegrationHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;
using Device.Application.Assignments.EventHandlers.IntegrationEvents;
using Device.Domain.Entities.DisplayAssignmentAggregate;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Core.Database;
using SharedKernel.Events;
using Shouldly;

namespace Device.UnitTests.Application.Assignments;

public sealed class RenderJobCompletedIntegrationHandlerTests
{
    [Fact]
    public async Task Handle_WhenAssignmentExistsInPending_ShouldMarkRenderedAndSave()
    {
        IDisplayAssignmentWriteRepository repository = Substitute.For<IDisplayAssignmentWriteRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        ILogger<RenderJobCompletedIntegrationHandler> logger = Substitute.For<ILogger<RenderJobCompletedIntegrationHandler>>();

        DisplayAssignment assignment = DisplayAssignment.Create(Guid.NewGuid(), "zone-a", "template-1", "Request", null, null, [new DisplayAssignmentZone(1, Guid.NewGuid())]).Value;
        repository.FindOneAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DisplayAssignment, bool>>>(), true, Arg.Any<CancellationToken>()).Returns(assignment);

        RenderJobCompletedIntegrationHandler handler = new(repository, unitOfWork, logger);
        await handler.Handle(new RenderJobCompletedIntegrationEvent(assignment.RenderJobId, assignment.DisplayId, new Uri("https://cdn.test/r.png")), TestContext.Current.CancellationToken);

        assignment.Status.ShouldBe(DisplayAssignmentStatus.Rendered);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAssignmentNotFound_ShouldNotSave()
    {
        IDisplayAssignmentWriteRepository repository = Substitute.For<IDisplayAssignmentWriteRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        ILogger<RenderJobCompletedIntegrationHandler> logger = Substitute.For<ILogger<RenderJobCompletedIntegrationHandler>>();
        repository.FindOneAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DisplayAssignment, bool>>>(), true, Arg.Any<CancellationToken>()).Returns((DisplayAssignment?)null);

        RenderJobCompletedIntegrationHandler handler = new(repository, unitOfWork, logger);
        await handler.Handle(new RenderJobCompletedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), new Uri("https://cdn.test/r.png")), TestContext.Current.CancellationToken);

        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
