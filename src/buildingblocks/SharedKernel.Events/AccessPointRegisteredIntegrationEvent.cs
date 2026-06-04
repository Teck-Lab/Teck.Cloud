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
        /// <summary>
        /// Gets or sets the access point identifier.
        /// </summary>
        public Guid AccessPointId { get; set; }

        /// <summary>
        /// Gets or sets the access point serial number.
        /// </summary>
        public string SerialNumber { get; set; } = default!;

        /// <summary>
        /// Gets or sets the access point vendor name.
        /// </summary>
        public string Vendor { get; set; } = default!;

        /// <summary>
        /// Gets or sets the location node identifier associated with the access point.
        /// </summary>
        public string LocationNodeId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the maximum capacity of the access point.
        /// </summary>
        public int MaxCapacity { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the access point was registered.
        /// </summary>
        public DateTimeOffset RegisteredAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessPointRegisteredIntegrationEvent"/> class.
        /// </summary>
        [MemoryPackConstructor]
        public AccessPointRegisteredIntegrationEvent() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessPointRegisteredIntegrationEvent"/> class.
        /// </summary>
        /// <param name="id">The access point identifier.</param>
        /// <param name="serial">The access point serial number.</param>
        /// <param name="vendor">The access point vendor name.</param>
        /// <param name="location">The location node identifier.</param>
        /// <param name="capacity">The maximum access point capacity.</param>
        /// <param name="at">The timestamp when registration occurred.</param>
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
