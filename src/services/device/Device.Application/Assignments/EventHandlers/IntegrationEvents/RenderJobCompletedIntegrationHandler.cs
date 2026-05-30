// <copyright file="RenderJobCompletedIntegrationHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;
using Device.Domain.Entities.DisplayAssignmentAggregate;
using ErrorOr;
using SharedKernel.Core.Database;
using SharedKernel.Events;

namespace Device.Application.Assignments.EventHandlers.IntegrationEvents;

/// <summary>
/// Consumes <see cref="RenderJobCompletedIntegrationEvent"/> published by the image-generator service
/// and transitions the matching <see cref="DisplayAssignment"/> from <c>Pending</c> to <c>Rendered</c>.
/// The lookup keys on <see cref="DisplayAssignment.RenderJobId"/> because render job ids are deterministic
/// per (assignment, version) and survive retries idempotently.
/// </summary>
public sealed partial class RenderJobCompletedIntegrationHandler
{
    private readonly IDisplayAssignmentWriteRepository displayAssignmentWriteRepository;
    private readonly IUnitOfWork unitOfWork;
    private readonly ILogger<RenderJobCompletedIntegrationHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderJobCompletedIntegrationHandler"/> class.
    /// </summary>
    /// <param name="displayAssignmentWriteRepository">The display assignment write repository.</param>
    /// <param name="unitOfWork">The unit of work used to persist the state transition.</param>
    /// <param name="logger">The logger instance.</param>
    public RenderJobCompletedIntegrationHandler(
        IDisplayAssignmentWriteRepository displayAssignmentWriteRepository,
        IUnitOfWork unitOfWork,
        ILogger<RenderJobCompletedIntegrationHandler> logger)
    {
        this.displayAssignmentWriteRepository = displayAssignmentWriteRepository;
        this.unitOfWork = unitOfWork;
        this.logger = logger;
    }

    /// <summary>
    /// Handles the render-job-completed integration event.
    /// </summary>
    /// <param name="integrationEvent">The integration event.</param>
    /// <param name="cancellationToken">The cancellation token propagated by the Wolverine runtime.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(RenderJobCompletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Tracking is required so EF persists the resulting state transition + domain events.
        DisplayAssignment? assignment = await this.displayAssignmentWriteRepository
            .FindOneAsync(
                assignment => assignment.RenderJobId == integrationEvent.JobId,
                enableTracking: true,
                cancellationToken)
            .ConfigureAwait(false);

        if (assignment is null)
        {
            // Idempotent: a missing assignment means the render outlived its owning assignment (e.g. cancelled
            // and recreated). The cross-service contract treats this as a benign no-op rather than a failure.
            LogAssignmentNotFound(this.logger, integrationEvent.JobId, integrationEvent.DisplayId);
            return;
        }

        ErrorOr<Success> transition = assignment.MarkRendered(
            integrationEvent.RenderedImageUri,
            DateTimeOffset.UtcNow);

        if (transition.IsError)
        {
            // Conflict means the assignment is not in Pending (already rendered/delivered/failed).
            // Logging at warning preserves observability without breaking the message contract.
            LogTransitionRejected(
                this.logger,
                assignment.Id,
                integrationEvent.JobId,
                transition.FirstError.Description);
            return;
        }

        await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogAssignmentMarkedRendered(this.logger, assignment.Id, integrationEvent.JobId);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "RenderJobCompleted received for unknown assignment. JobId={JobId} DisplayId={DisplayId}")]
    private static partial void LogAssignmentNotFound(ILogger logger, Guid jobId, Guid displayId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "MarkRendered rejected for assignment {AssignmentId} (job {JobId}): {Reason}")]
    private static partial void LogTransitionRejected(ILogger logger, Guid assignmentId, Guid jobId, string reason);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Assignment {AssignmentId} marked Rendered from job {JobId}")]
    private static partial void LogAssignmentMarkedRendered(ILogger logger, Guid assignmentId, Guid jobId);
}
