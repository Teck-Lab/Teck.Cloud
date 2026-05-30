// <copyright file="DisplayOperationCompletedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events
{
    /// <summary>
    /// Cross-service notification that an operation targeting a display has completed.
    /// </summary>
    [MemoryPackable]
    public partial class DisplayOperationCompletedIntegrationEvent : IntegrationEvent
    {
        /// <summary>
        /// Gets or sets the display identifier.
        /// </summary>
        public Guid DisplayId { get; set; }

        /// <summary>
        /// Gets or sets the operation type.
        /// </summary>
        public string OperationType { get; set; } = default!;

        /// <summary>
        /// Gets or sets when the operation completed.
        /// </summary>
        public DateTimeOffset CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets an optional result payload.
        /// </summary>
        public string? ResultPayload { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayOperationCompletedIntegrationEvent"/> class.
        /// </summary>
        [MemoryPackConstructor]
        public DisplayOperationCompletedIntegrationEvent()
        {
            // Parameterless constructor for message broker serialization.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayOperationCompletedIntegrationEvent"/> class.
        /// </summary>
        public DisplayOperationCompletedIntegrationEvent(Guid displayId, string operationType, DateTimeOffset completedAt, string? resultPayload)
        {
            DisplayId = displayId;
            OperationType = operationType;
            CompletedAt = completedAt;
            ResultPayload = resultPayload;
        }
    }
}
