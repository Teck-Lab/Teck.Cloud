// <copyright file="AccessPointLoadChangedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events
{
    /// <summary>
    /// Cross-service notification that an access point's load changed.
    /// </summary>
    [MemoryPackable]
    public partial class AccessPointLoadChangedIntegrationEvent : IntegrationEvent
    {
        public Guid AccessPointId { get; set; }

        public string SerialNumber { get; set; } = default!;

        public string LocationNodeId { get; set; } = default!;

        public int PreviousLoad { get; set; }

        public int NewLoad { get; set; }

        public int MaxCapacity { get; set; }

        public DateTimeOffset ChangedAt { get; set; }

        [MemoryPackConstructor]
        public AccessPointLoadChangedIntegrationEvent() { }

        public AccessPointLoadChangedIntegrationEvent(Guid id, string serial, string location, int previous, int newLoad, int capacity, DateTimeOffset at)
        {
            AccessPointId = id;
            SerialNumber = serial;
            LocationNodeId = location;
            PreviousLoad = previous;
            NewLoad = newLoad;
            MaxCapacity = capacity;
            ChangedAt = at;
        }
    }
}
