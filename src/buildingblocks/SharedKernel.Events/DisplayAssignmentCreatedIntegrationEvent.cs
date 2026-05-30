// <copyright file="DisplayAssignmentCreatedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events
{
    /// <summary>
    /// Cross-service notification that a display assignment was created and a render job is pending.
    /// Consumers can react by preparing downstream context (e.g., projecting read models, prewarming caches).
    /// </summary>
    [MemoryPackable]
    public partial class DisplayAssignmentCreatedIntegrationEvent : IntegrationEvent
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
        /// Gets or sets the deterministic render job identifier.
        /// </summary>
        public Guid RenderJobId { get; set; }

        /// <summary>
        /// Gets or sets the assignment version (monotonic per display).
        /// </summary>
        public int AssignmentVersion { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayAssignmentCreatedIntegrationEvent"/> class.
        /// </summary>
        [MemoryPackConstructor]
        public DisplayAssignmentCreatedIntegrationEvent()
        {
            // Parameterless constructor for message broker serialization.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayAssignmentCreatedIntegrationEvent"/> class.
        /// </summary>
        /// <param name="assignmentId">The assignment identifier.</param>
        /// <param name="displayId">The owning display identifier.</param>
        /// <param name="renderJobId">The deterministic render job identifier.</param>
        /// <param name="assignmentVersion">The assignment version.</param>
        public DisplayAssignmentCreatedIntegrationEvent(
            Guid assignmentId,
            Guid displayId,
            Guid renderJobId,
            int assignmentVersion)
        {
            AssignmentId = assignmentId;
            DisplayId = displayId;
            RenderJobId = renderJobId;
            AssignmentVersion = assignmentVersion;
        }
    }
}
