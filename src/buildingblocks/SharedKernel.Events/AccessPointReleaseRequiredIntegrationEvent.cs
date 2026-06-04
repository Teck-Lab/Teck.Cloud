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
        /// <summary>
        /// Gets or sets the display identifier that reserved the access point.
        /// </summary>
        public Guid DisplayId { get; set; }

        /// <summary>
        /// Gets or sets the serial number of the access point to release.
        /// </summary>
        public string AccessPointSerial { get; set; } = default!;

        /// <summary>
        /// Gets or sets the timestamp when release is required.
        /// </summary>
        public DateTimeOffset ReleasedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessPointReleaseRequiredIntegrationEvent"/> class.
        /// </summary>
        [MemoryPackConstructor]
        public AccessPointReleaseRequiredIntegrationEvent() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessPointReleaseRequiredIntegrationEvent"/> class.
        /// </summary>
        /// <param name="displayId">The display identifier that reserved the access point.</param>
        /// <param name="accessPointSerial">The access point serial number to release.</param>
        /// <param name="releasedAt">The timestamp when release is required.</param>
        public AccessPointReleaseRequiredIntegrationEvent(Guid displayId, string accessPointSerial, DateTimeOffset releasedAt)
        {
            DisplayId = displayId;
            AccessPointSerial = accessPointSerial;
            ReleasedAt = releasedAt;
        }
    }
}
