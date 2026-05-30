// <copyright file="DisplayAssignmentDeliveredIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events
{
    /// <summary>
    /// Cross-service notification that a vendor device server acknowledged delivery of the rendered
    /// image to the physical ESL. Terminal success state for the assignment lifecycle.
    /// </summary>
    [MemoryPackable]
    public partial class DisplayAssignmentDeliveredIntegrationEvent : IntegrationEvent
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
        /// Gets or sets the ESL provider key that performed the delivery.
        /// </summary>
        public string EslProvider { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayAssignmentDeliveredIntegrationEvent"/> class.
        /// </summary>
        [MemoryPackConstructor]
        public DisplayAssignmentDeliveredIntegrationEvent()
        {
            // Parameterless constructor for message broker serialization.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayAssignmentDeliveredIntegrationEvent"/> class.
        /// </summary>
        /// <param name="assignmentId">The assignment identifier.</param>
        /// <param name="displayId">The owning display identifier.</param>
        /// <param name="eslProvider">The ESL provider key.</param>
        public DisplayAssignmentDeliveredIntegrationEvent(
            Guid assignmentId,
            Guid displayId,
            string eslProvider)
        {
            AssignmentId = assignmentId;
            DisplayId = displayId;
            EslProvider = eslProvider;
        }
    }
}
