// <copyright file="AccessPointStatusChangedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events
{
    /// <summary>
    /// Cross-service notification that an access point's status changed.
    /// </summary>
    [MemoryPackable]
    public partial class AccessPointStatusChangedIntegrationEvent : IntegrationEvent
    {
        /// <summary>
        /// Gets or sets the access point identifier.
        /// </summary>
        public Guid AccessPointId { get; set; }

        /// <summary>
        /// Gets or sets the access point serial number.
        /// </summary>
        public string SerialNumber { get; set; } = default!;

        /// <summary>
        /// Gets or sets the previous status value before the change.
        /// </summary>
        public string PreviousStatus { get; set; } = default!;

        /// <summary>
        /// Gets or sets the new status value after the change.
        /// </summary>
        public string NewStatus { get; set; } = default!;

        /// <summary>
        /// Gets or sets the location node identifier associated with the access point.
        /// </summary>
        public string LocationNodeId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the timestamp when the status changed.
        /// </summary>
        public DateTimeOffset ChangedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessPointStatusChangedIntegrationEvent"/> class.
        /// </summary>
        [MemoryPackConstructor]
        public AccessPointStatusChangedIntegrationEvent() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessPointStatusChangedIntegrationEvent"/> class.
        /// </summary>
        /// <param name="id">The access point identifier.</param>
        /// <param name="serial">The access point serial number.</param>
        /// <param name="previous">The previous access point status.</param>
        /// <param name="newStatus">The new access point status.</param>
        /// <param name="location">The location node identifier.</param>
        /// <param name="at">The timestamp when the status change occurred.</param>
        public AccessPointStatusChangedIntegrationEvent(Guid id, string serial, string previous, string newStatus, string location, DateTimeOffset at)
        {
            AccessPointId = id;
            SerialNumber = serial;
            PreviousStatus = previous;
            NewStatus = newStatus;
            LocationNodeId = location;
            ChangedAt = at;
        }
    }
}
