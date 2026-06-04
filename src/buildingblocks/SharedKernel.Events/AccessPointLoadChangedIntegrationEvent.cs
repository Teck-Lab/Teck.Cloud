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
        /// <summary>
        /// Gets or sets the access point identifier.
        /// </summary>
        public Guid AccessPointId { get; set; }

        /// <summary>
        /// Gets or sets the access point serial number.
        /// </summary>
        public string SerialNumber { get; set; } = default!;

        /// <summary>
        /// Gets or sets the location node identifier associated with the access point.
        /// </summary>
        public string LocationNodeId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the previous load value before the change.
        /// </summary>
        public int PreviousLoad { get; set; }

        /// <summary>
        /// Gets or sets the new load value after the change.
        /// </summary>
        public int NewLoad { get; set; }

        /// <summary>
        /// Gets or sets the maximum capacity of the access point.
        /// </summary>
        public int MaxCapacity { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the load changed.
        /// </summary>
        public DateTimeOffset ChangedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessPointLoadChangedIntegrationEvent"/> class.
        /// </summary>
        [MemoryPackConstructor]
        public AccessPointLoadChangedIntegrationEvent() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessPointLoadChangedIntegrationEvent"/> class.
        /// </summary>
        /// <param name="id">The access point identifier.</param>
        /// <param name="serial">The access point serial number.</param>
        /// <param name="location">The location node identifier.</param>
        /// <param name="previous">The previous load value.</param>
        /// <param name="newLoad">The new load value.</param>
        /// <param name="capacity">The maximum access point capacity.</param>
        /// <param name="at">The timestamp when the change occurred.</param>
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
