// <copyright file="EslDispatchCompletedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events
{
    /// <summary>
    /// Cross-service notification that a vendor ESL worker successfully dispatched the rendered image
    /// to the physical device. The owning service consumes this to transition the assignment to Delivered.
    /// </summary>
    [MemoryPackable]
    public partial class EslDispatchCompletedIntegrationEvent : IntegrationEvent
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
        /// Gets or sets the ESL provider key that performed the dispatch.
        /// </summary>
        public string EslProvider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC timestamp when the dispatch completed.
        /// </summary>
        public DateTimeOffset DispatchedAt { get; set; }

        /// <summary>
        /// Gets or sets the serial number of the access point reserved for this dispatch.
        /// </summary>
        public string? AccessPointSerial { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EslDispatchCompletedIntegrationEvent"/> class.
        /// </summary>
        [MemoryPackConstructor]
        public EslDispatchCompletedIntegrationEvent()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EslDispatchCompletedIntegrationEvent"/> class.
        /// </summary>
        /// <param name="assignmentId">The assignment identifier.</param>
        /// <param name="displayId">The owning display identifier.</param>
        /// <param name="eslProvider">The ESL provider key.</param>
        /// <param name="dispatchedAt">The dispatch completion timestamp.</param>
        /// <param name="accessPointSerial">The reserved access point serial number.</param>
        public EslDispatchCompletedIntegrationEvent(
            Guid assignmentId,
            Guid displayId,
            string eslProvider,
            DateTimeOffset dispatchedAt,
            string? accessPointSerial)
        {
            AssignmentId = assignmentId;
            DisplayId = displayId;
            EslProvider = eslProvider;
            DispatchedAt = dispatchedAt;
            AccessPointSerial = accessPointSerial;
        }
    }
}
