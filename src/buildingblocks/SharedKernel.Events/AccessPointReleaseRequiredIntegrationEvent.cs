// <copyright file="AccessPointReleaseRequiredIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events
{
    /// <summary>
    /// Cross-service notification that a reserved access point load must be released.
    /// </summary>
    [MemoryPackable]
    public partial class AccessPointReleaseRequiredIntegrationEvent : IntegrationEvent
    {
        public Guid DisplayId { get; set; }

        public string AccessPointSerial { get; set; } = default!;

        public DateTimeOffset ReleasedAt { get; set; }

        [MemoryPackConstructor]
        public AccessPointReleaseRequiredIntegrationEvent() { }

        public AccessPointReleaseRequiredIntegrationEvent(Guid displayId, string accessPointSerial, DateTimeOffset releasedAt)
        {
            DisplayId = displayId;
            AccessPointSerial = accessPointSerial;
            ReleasedAt = releasedAt;
        }
    }
}
