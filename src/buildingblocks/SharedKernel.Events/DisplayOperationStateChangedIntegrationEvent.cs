// <copyright file="DisplayOperationStateChangedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events
{
    /// <summary>
    /// Cross-service notification that an operation's state for a display changed (queued, started, completed, failed).
    /// </summary>
    [MemoryPackable]
    public partial class DisplayOperationStateChangedIntegrationEvent : IntegrationEvent
    {
        /// <summary>
        /// Gets or sets the display identifier.
        /// </summary>
        public Guid DisplayId { get; set; }

        /// <summary>
        /// Gets or sets the location node identifier.
        /// </summary>
        public string LocationNodeId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the operation type.
        /// </summary>
        public string OperationType { get; set; } = default!;

        /// <summary>
        /// Gets or sets the status (Started|Queued|Completed|Failed).
        /// </summary>
        public string Status { get; set; } = default!;

        /// <summary>
        /// Gets or sets the queue depth for this operation type at the location.
        /// </summary>
        public int QueueDepth { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the state change.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayOperationStateChangedIntegrationEvent"/> class.
        /// </summary>
        [MemoryPackConstructor]
        public DisplayOperationStateChangedIntegrationEvent()
        {
            // Parameterless constructor for message broker serialization.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayOperationStateChangedIntegrationEvent"/> class.
        /// </summary>
        public DisplayOperationStateChangedIntegrationEvent(Guid displayId, string locationNodeId, string tenantId, string operationType, string status, int queueDepth, DateTimeOffset timestamp)
        {
            DisplayId = displayId;
            LocationNodeId = locationNodeId;
            TenantId = tenantId;
            OperationType = operationType;
            Status = status;
            QueueDepth = queueDepth;
            Timestamp = timestamp;
        }
    }
}
