// <copyright file="AccessPointRegisteredIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events
{
    /// <summary>
    /// Cross-service notification that an access point was registered.
    /// </summary>
    [MemoryPackable]
    public partial class AccessPointRegisteredIntegrationEvent : IntegrationEvent
    {
        public Guid AccessPointId { get; set; }

        public string SerialNumber { get; set; } = default!;

        public string Vendor { get; set; } = default!;

        public string LocationNodeId { get; set; } = default!;

        public int MaxCapacity { get; set; }

        public DateTimeOffset RegisteredAt { get; set; }

        [MemoryPackConstructor]
        public AccessPointRegisteredIntegrationEvent() { }

        public AccessPointRegisteredIntegrationEvent(Guid id, string serial, string vendor, string location, int capacity, DateTimeOffset at)
        {
            AccessPointId = id;
            SerialNumber = serial;
            Vendor = vendor;
            LocationNodeId = location;
            MaxCapacity = capacity;
            RegisteredAt = at;
        }
    }
}
