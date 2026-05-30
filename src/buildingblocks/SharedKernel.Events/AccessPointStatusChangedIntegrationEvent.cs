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
        public Guid AccessPointId { get; set; }

        public string SerialNumber { get; set; } = default!;

        public string PreviousStatus { get; set; } = default!;

        public string NewStatus { get; set; } = default!;

        public string LocationNodeId { get; set; } = default!;

        public DateTimeOffset ChangedAt { get; set; }

        [MemoryPackConstructor]
        public AccessPointStatusChangedIntegrationEvent() { }

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
