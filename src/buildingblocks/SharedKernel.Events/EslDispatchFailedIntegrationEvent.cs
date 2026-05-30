// <copyright file="EslDispatchFailedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events
{
    /// <summary>
    /// Cross-service notification that a vendor ESL worker failed to dispatch the rendered image
    /// to the physical device. The owning service consumes this to transition the assignment to Failed.
    /// </summary>
    [MemoryPackable]
    public partial class EslDispatchFailedIntegrationEvent : IntegrationEvent
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
        /// Gets or sets the ESL provider key that attempted the dispatch.
        /// </summary>
        public string EslProvider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the failure reason supplied by the vendor adapter.
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC timestamp when the dispatch failed.
        /// </summary>
        public DateTimeOffset FailedAt { get; set; }

        /// <summary>
        /// Gets or sets the serial number of the access point reserved for this dispatch.
        /// </summary>
        public string? AccessPointSerial { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EslDispatchFailedIntegrationEvent"/> class.
        /// </summary>
        [MemoryPackConstructor]
        public EslDispatchFailedIntegrationEvent()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EslDispatchFailedIntegrationEvent"/> class.
        /// </summary>
        /// <param name="assignmentId">The assignment identifier.</param>
        /// <param name="displayId">The owning display identifier.</param>
        /// <param name="eslProvider">The ESL provider key.</param>
        /// <param name="reason">The failure reason.</param>
        /// <param name="failedAt">The dispatch failure timestamp.</param>
        /// <param name="accessPointSerial">The reserved access point serial number.</param>
        public EslDispatchFailedIntegrationEvent(
            Guid assignmentId,
            Guid displayId,
            string eslProvider,
            string reason,
            DateTimeOffset failedAt,
            string? accessPointSerial)
        {
            AssignmentId = assignmentId;
            DisplayId = displayId;
            EslProvider = eslProvider;
            Reason = reason;
            FailedAt = failedAt;
            AccessPointSerial = accessPointSerial;
        }
    }
}
