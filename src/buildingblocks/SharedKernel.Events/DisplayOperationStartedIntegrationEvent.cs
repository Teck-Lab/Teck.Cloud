// <copyright file="DisplayOperationStartedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events
{
    /// <summary>
    /// Cross-service notification that an operation targeting a display has started.
    /// </summary>
    [MemoryPackable]
    public partial class DisplayOperationStartedIntegrationEvent : IntegrationEvent
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
        /// Gets or sets the operation type.
        /// </summary>
        public string OperationType { get; set; } = default!;

        /// <summary>
        /// Gets or sets when the operation started.
        /// </summary>
        public DateTimeOffset StartedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayOperationStartedIntegrationEvent"/> class.
        /// </summary>
        [MemoryPackConstructor]
        public DisplayOperationStartedIntegrationEvent()
        {
            // Parameterless constructor for message broker serialization.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayOperationStartedIntegrationEvent"/> class.
        /// </summary>
        public DisplayOperationStartedIntegrationEvent(Guid displayId, string locationNodeId, string operationType, DateTimeOffset startedAt)
        {
            DisplayId = displayId;
            LocationNodeId = locationNodeId;
            OperationType = operationType;
            StartedAt = startedAt;
        }
    }
}
