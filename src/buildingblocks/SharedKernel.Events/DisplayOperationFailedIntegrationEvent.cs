// <copyright file="DisplayOperationFailedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events
{
    /// <summary>
    /// Cross-service notification that an operation targeting a display has failed.
    /// </summary>
    [MemoryPackable]
    public partial class DisplayOperationFailedIntegrationEvent : IntegrationEvent
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
        /// Gets or sets when the operation failed.
        /// </summary>
        public DateTimeOffset FailedAt { get; set; }

        /// <summary>
        /// Gets or sets the failure reason.
        /// </summary>
        public string Reason { get; set; } = default!;

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayOperationFailedIntegrationEvent"/> class.
        /// </summary>
        [MemoryPackConstructor]
        public DisplayOperationFailedIntegrationEvent()
        {
            // Parameterless constructor for message broker serialization.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayOperationFailedIntegrationEvent"/> class.
        /// </summary>
        public DisplayOperationFailedIntegrationEvent(Guid displayId, string operationType, DateTimeOffset failedAt, string reason)
        {
            DisplayId = displayId;
            OperationType = operationType;
            FailedAt = failedAt;
            Reason = reason;
        }
    }
}
