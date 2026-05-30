// <copyright file="DisplayAssignment.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DisplayAssignmentAggregate.Events;
using ErrorOr;
using SharedKernel.Core.Domain;

namespace Device.Domain.Entities.DisplayAssignmentAggregate;

/// <summary>
/// Represents a zoned product-and-template assignment applied to a single <see cref="DisplayAggregate.Display"/>.
/// The aggregate owns its zone bindings and drives the render -> vendor-delivery state machine.
/// </summary>
public sealed class DisplayAssignment : BaseEntity, IAggregateRoot
{
    private readonly List<DisplayAssignmentZone> _zones = [];

    private DisplayAssignment()
    {
    }

    /// <summary>
    /// Gets the display this assignment is bound to.
    /// </summary>
    public Guid DisplayId { get; private set; }

    /// <summary>
    /// Gets the location node identifier captured at assignment time.
    /// </summary>
    public string LocationNodeId { get; private set; } = default!;

    /// <summary>
    /// Gets the resolved template identifier used for rendering.
    /// </summary>
    public string ResolvedTemplateId { get; private set; } = default!;

    /// <summary>
    /// Gets the source of the resolved template (e.g. "Request" or "Inherited").
    /// </summary>
    public string TemplateSource { get; private set; } = default!;

    /// <summary>
    /// Gets the monotonically increasing version. Combined with the entity identifier it forms <see cref="RenderJobId"/>.
    /// </summary>
    public int AssignmentVersion { get; private set; }

    /// <summary>
    /// Gets the deterministic render job identifier derived from (assignment identifier, <see cref="AssignmentVersion"/>).
    /// Enables idempotent re-enqueue of the same render.
    /// </summary>
    public Guid RenderJobId { get; private set; }

    /// <summary>
    /// Gets the current lifecycle status.
    /// </summary>
    public DisplayAssignmentStatus Status { get; private set; }

    /// <summary>
    /// Gets the URI of the rendered image once <see cref="Status"/> reaches <see cref="DisplayAssignmentStatus.Rendered"/>.
    /// </summary>
    public Uri? RenderedImageUri { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when rendering completed.
    /// </summary>
    public DateTimeOffset? RenderedAtUtc { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when vendor delivery completed.
    /// </summary>
    public DateTimeOffset? DeliveredAtUtc { get; private set; }

    /// <summary>
    /// Gets the failure reason when <see cref="Status"/> is <see cref="DisplayAssignmentStatus.Failed"/>.
    /// </summary>
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Gets the resolved template design snapshot captured at assignment time for reproducibility.
    /// </summary>
    public string? TemplateSnapshot { get; private set; }

    /// <summary>
    /// Gets the product data snapshot captured at assignment time for reproducibility.
    /// </summary>
    public string? ProductDataSnapshot { get; private set; }

    /// <summary>
    /// Gets the zone bindings owned by this assignment.
    /// </summary>
    public IReadOnlyList<DisplayAssignmentZone> Zones => _zones;

    /// <summary>
    /// Creates a new <see cref="DisplayAssignment"/> in the <see cref="DisplayAssignmentStatus.Pending"/> state.
    /// </summary>
    /// <param name="displayId">The display being assigned.</param>
    /// <param name="locationNodeId">Location node captured at assignment time.</param>
    /// <param name="resolvedTemplateId">The resolved template identifier.</param>
    /// <param name="templateSource">Where the template was resolved from.</param>
    /// <param name="templateSnapshot">The resolved template design JSON snapshot.</param>
    /// <param name="productDataSnapshot">The product data JSON snapshot.</param>
    /// <param name="zones">The zone-to-product bindings.</param>
    /// <returns>The created assignment or validation errors.</returns>
    public static ErrorOr<DisplayAssignment> Create(
        Guid displayId,
        string locationNodeId,
        string resolvedTemplateId,
        string templateSource,
        string? templateSnapshot,
        string? productDataSnapshot,
        IReadOnlyCollection<DisplayAssignmentZone> zones)
    {
        if (displayId == Guid.Empty)
        {
            return Error.Validation("DisplayAssignment.DisplayIdRequired", "DisplayId is required.");
        }

        if (string.IsNullOrWhiteSpace(locationNodeId))
        {
            return Error.Validation("DisplayAssignment.LocationNodeIdRequired", "LocationNodeId is required.");
        }

        if (string.IsNullOrWhiteSpace(resolvedTemplateId))
        {
            return Error.Validation("DisplayAssignment.TemplateRequired", "ResolvedTemplateId is required.");
        }

        ArgumentNullException.ThrowIfNull(zones);

        if (zones.Count == 0)
        {
            return Error.Validation("DisplayAssignment.ZonesRequired", "At least one zone binding is required.");
        }

        DisplayAssignment assignment = new()
        {
            DisplayId = displayId,
            LocationNodeId = locationNodeId,
            ResolvedTemplateId = resolvedTemplateId,
            TemplateSource = string.IsNullOrWhiteSpace(templateSource) ? "Request" : templateSource,
            TemplateSnapshot = templateSnapshot,
            ProductDataSnapshot = productDataSnapshot,
            AssignmentVersion = 1,
            Status = DisplayAssignmentStatus.Pending,
        };

        foreach (DisplayAssignmentZone zone in zones)
        {
            assignment._zones.Add(zone);
        }

        assignment.RenderJobId = DeriveRenderJobId(assignment.Id, assignment.AssignmentVersion);
        assignment.AddDomainEvent(new DisplayAssignmentCreatedEvent(assignment.Id, displayId, assignment.RenderJobId));

        return assignment;
    }

    /// <summary>
    /// Transitions from <see cref="DisplayAssignmentStatus.Pending"/> to <see cref="DisplayAssignmentStatus.Rendered"/>.
    /// </summary>
    /// <param name="renderedImageUri">The URI of the rendered image.</param>
    /// <param name="renderedAtUtc">When the render completed.</param>
    /// <returns>Success or a transition error.</returns>
    public ErrorOr<Success> MarkRendered(Uri renderedImageUri, DateTimeOffset renderedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(renderedImageUri);

        if (Status is not DisplayAssignmentStatus.Pending)
        {
            return Error.Conflict(
                "DisplayAssignment.InvalidTransitionToRendered",
                $"Cannot transition from {Status} to Rendered.");
        }

        RenderedImageUri = renderedImageUri;
        RenderedAtUtc = renderedAtUtc;
        Status = DisplayAssignmentStatus.Rendered;
        AddDomainEvent(new DisplayAssignmentRenderedEvent(Id, DisplayId, renderedImageUri));

        return Result.Success;
    }

    /// <summary>
    /// Transitions from <see cref="DisplayAssignmentStatus.Rendered"/> to <see cref="DisplayAssignmentStatus.Delivered"/>.
    /// </summary>
    /// <param name="deliveredAtUtc">When vendor delivery completed.</param>
    /// <returns>Success or a transition error.</returns>
    public ErrorOr<Success> MarkDelivered(DateTimeOffset deliveredAtUtc)
    {
        if (Status is not DisplayAssignmentStatus.Rendered)
        {
            return Error.Conflict(
                "DisplayAssignment.InvalidTransitionToDelivered",
                $"Cannot transition from {Status} to Delivered.");
        }

        DeliveredAtUtc = deliveredAtUtc;
        Status = DisplayAssignmentStatus.Delivered;
        AddDomainEvent(new DisplayAssignmentDeliveredEvent(Id, DisplayId));

        return Result.Success;
    }

    /// <summary>
    /// Transitions to <see cref="DisplayAssignmentStatus.Failed"/> from any non-terminal state.
    /// </summary>
    /// <param name="failureReason">A short, diagnostic-friendly reason.</param>
    /// <returns>Success or a transition error.</returns>
    public ErrorOr<Success> MarkFailed(string failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
        {
            return Error.Validation("DisplayAssignment.FailureReasonRequired", "FailureReason is required.");
        }

        if (Status is DisplayAssignmentStatus.Delivered or DisplayAssignmentStatus.Failed)
        {
            return Error.Conflict(
                "DisplayAssignment.InvalidTransitionToFailed",
                $"Cannot transition from terminal state {Status} to Failed.");
        }

        FailureReason = failureReason;
        Status = DisplayAssignmentStatus.Failed;
        AddDomainEvent(new DisplayAssignmentFailedEvent(Id, DisplayId, failureReason));

        return Result.Success;
    }

    // Deterministic Guid derived from (AssignmentId, AssignmentVersion) so re-enqueuing the same
    // render request stays idempotent across retries. Uses XOR-based mixing rather than crypto hashing
    // because uniqueness within an aggregate is what matters, not collision resistance against attackers.
    private static Guid DeriveRenderJobId(Guid assignmentId, int version)
    {
        Span<byte> bytes = stackalloc byte[16];
        if (!assignmentId.TryWriteBytes(bytes))
        {
            return Guid.NewGuid();
        }

        bytes[12] ^= (byte)(version & 0xFF);
        bytes[13] ^= (byte)((version >> 8) & 0xFF);
        bytes[14] ^= (byte)((version >> 16) & 0xFF);
        bytes[15] ^= (byte)((version >> 24) & 0xFF);
        return new Guid(bytes);
    }
}
