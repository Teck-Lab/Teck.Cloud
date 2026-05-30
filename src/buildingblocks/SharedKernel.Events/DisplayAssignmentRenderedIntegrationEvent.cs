// <copyright file="DisplayAssignmentRenderedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events
{
    /// <summary>
    /// Cross-service notification that the image-generator finished producing the rendered image
    /// for a display assignment. Vendor device-server workers consume this to dispatch to the physical ESL.
    /// </summary>
    [MemoryPackable]
    public partial class DisplayAssignmentRenderedIntegrationEvent : IntegrationEvent
    {
        /// <summary>
        /// Gets or sets the assignment identifier.
        /// </summary>
        public Guid AssignmentId { get; set; }

        /// <summary>
        /// Gets or sets the owning display identifier.
        /// </summary>
        public Guid DisplayId { get; set; }

        /// <summary>
        /// Gets or sets the render job identifier.
        /// </summary>
        public Guid RenderJobId { get; set; }

        /// <summary>
        /// Gets or sets the rendered image URI (blob URI or local path).
        /// </summary>
        public Uri RenderedImageUri { get; set; } = default!;

        /// <summary>
        /// Gets or sets the ESL provider key (e.g., "Hanshow", "Stub"). Drives vendor-worker routing.
        /// </summary>
        public string EslProvider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the serial number of the access point reserved for this dispatch.
        /// </summary>
        public string? AccessPointSerial { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayAssignmentRenderedIntegrationEvent"/> class.
        /// </summary>
        [MemoryPackConstructor]
        public DisplayAssignmentRenderedIntegrationEvent()
        {
            // Parameterless constructor for message broker serialization.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayAssignmentRenderedIntegrationEvent"/> class.
        /// </summary>
        /// <param name="assignmentId">The assignment identifier.</param>
        /// <param name="displayId">The owning display identifier.</param>
        /// <param name="renderJobId">The render job identifier.</param>
        /// <param name="renderedImageUri">The rendered image URI.</param>
        /// <param name="eslProvider">The ESL provider key.</param>
        /// <param name="accessPointSerial">The reserved access point serial number.</param>
        public DisplayAssignmentRenderedIntegrationEvent(
            Guid assignmentId,
            Guid displayId,
            Guid renderJobId,
            Uri renderedImageUri,
            string eslProvider,
            string? accessPointSerial)
        {
            AssignmentId = assignmentId;
            DisplayId = displayId;
            RenderJobId = renderJobId;
            RenderedImageUri = renderedImageUri;
            EslProvider = eslProvider;
            AccessPointSerial = accessPointSerial;
        }
    }
}
