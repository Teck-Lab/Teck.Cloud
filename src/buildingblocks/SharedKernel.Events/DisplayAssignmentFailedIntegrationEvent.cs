// <copyright file="DisplayAssignmentFailedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events
{
    /// <summary>
    /// Cross-service notification that an assignment terminated in a failure state
    /// during rendering or vendor delivery.
    /// </summary>
    [MemoryPackable]
    public partial class DisplayAssignmentFailedIntegrationEvent : IntegrationEvent
    {
        /// <summary>
        /// Gets or sets the assignment identifier.
        /// </summary>
        public Guid AssignmentId { get; set; }

        /// <summary>
        /// Gets or sets the owning display identifier.
        /// </summary>
        public Guid DisplayId { get; set; }

        /// <summary>
        /// Gets or sets the short failure reason for diagnostics.
        /// </summary>
        public string FailureReason { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the lifecycle stage at which the failure occurred ("Render" or "Delivery").
        /// </summary>
        public string FailedStage { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayAssignmentFailedIntegrationEvent"/> class.
        /// </summary>
        [MemoryPackConstructor]
        public DisplayAssignmentFailedIntegrationEvent()
        {
            // Parameterless constructor for message broker serialization.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayAssignmentFailedIntegrationEvent"/> class.
        /// </summary>
        /// <param name="assignmentId">The assignment identifier.</param>
        /// <param name="displayId">The owning display identifier.</param>
        /// <param name="failureReason">The short failure reason.</param>
        /// <param name="failedStage">The lifecycle stage at which the failure occurred.</param>
        public DisplayAssignmentFailedIntegrationEvent(
            Guid assignmentId,
            Guid displayId,
            string failureReason,
            string failedStage)
        {
            AssignmentId = assignmentId;
            DisplayId = displayId;
            FailureReason = failureReason;
            FailedStage = failedStage;
        }
    }
}
