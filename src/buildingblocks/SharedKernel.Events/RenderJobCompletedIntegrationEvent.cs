// <copyright file="RenderJobCompletedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events
{
    /// <summary>
    /// Cross-service notification emitted by the image-generator when a render job completes successfully.
    /// The Device service consumes this to transition the corresponding <c>DisplayAssignment</c> to the
    /// rendered state and record the rendered image URI.
    /// </summary>
    [MemoryPackable]
    public partial class RenderJobCompletedIntegrationEvent : IntegrationEvent
    {
        /// <summary>
        /// Gets or sets the render job identifier (matches the deterministic id assigned at creation time).
        /// </summary>
        public Guid JobId { get; set; }

        /// <summary>
        /// Gets or sets the owning display identifier.
        /// </summary>
        public Guid DisplayId { get; set; }

        /// <summary>
        /// Gets or sets the URI of the rendered image (file URI or blob URI depending on storage backend).
        /// </summary>
        public Uri RenderedImageUri { get; set; } = default!;

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderJobCompletedIntegrationEvent"/> class.
        /// </summary>
        [MemoryPackConstructor]
        public RenderJobCompletedIntegrationEvent()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderJobCompletedIntegrationEvent"/> class.
        /// </summary>
        /// <param name="jobId">The render job identifier.</param>
        /// <param name="displayId">The owning display identifier.</param>
        /// <param name="renderedImageUri">The rendered image URI.</param>
        public RenderJobCompletedIntegrationEvent(Guid jobId, Guid displayId, Uri renderedImageUri)
        {
            JobId = jobId;
            DisplayId = displayId;
            RenderedImageUri = renderedImageUri;
        }
    }
}
