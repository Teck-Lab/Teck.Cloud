// <copyright file="PreviewDeviceAssignment.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Device.Application.Assignments.Features.PreviewDeviceAssignment.V1;

/// <summary>
/// Query to preview a device assignment without persisting it.
/// </summary>
/// <param name="DeviceId">The target device identifier.</param>
/// <param name="LocationNodeId">The location node identifier.</param>
/// <param name="TemplateId">The optional explicit template identifier.</param>
/// <param name="Zones">The requested zone assignments.</param>
public sealed record PreviewDeviceAssignmentQuery(
    string DeviceId,
    string LocationNodeId,
    string? TemplateId,
    IReadOnlyList<PreviewDeviceAssignmentZoneRequest> Zones)
    : IQuery<ErrorOr<PreviewDeviceAssignmentResponse>>;

/// <summary>
/// Handles <see cref="PreviewDeviceAssignmentQuery"/>.
/// </summary>
public sealed class PreviewDeviceAssignmentQueryHandler(
    IDeviceDefinitionReadRepository deviceDefinitionReadRepository,
    ILocationTemplateContextRunner locationTemplateContextRunner)
    : IQueryHandler<PreviewDeviceAssignmentQuery, ErrorOr<PreviewDeviceAssignmentResponse>>
{
    private readonly IDeviceDefinitionReadRepository deviceDefinitionReadRepository = deviceDefinitionReadRepository;
    private readonly ILocationTemplateContextRunner locationTemplateContextRunner = locationTemplateContextRunner;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<PreviewDeviceAssignmentResponse>> Handle(
        PreviewDeviceAssignmentQuery request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.DeviceId, out Guid displayId))
        {
            return Error.Validation(
                code: "Device.InvalidDeviceId",
                description: $"'{request.DeviceId}' is not a valid device identifier.");
        }

        DisplayLayoutContext? layoutContext = await this.deviceDefinitionReadRepository
            .GetLayoutContextByDisplayIdAsync(displayId, cancellationToken)
            .ConfigureAwait(false);

        if (layoutContext is null)
        {
            return Error.NotFound(
                code: "Device.LayoutNotFound",
                description: $"No layout context found for device '{request.DeviceId}'.");
        }

        if (request.Zones.Select(zone => zone.ZoneIndex).Distinct().Count() != request.Zones.Count)
        {
            return Error.Validation(
                code: "Device.DuplicateZoneIndex",
                description: "Each zone index must be unique.");
        }

        if (request.Zones.Count > layoutContext.MaxZoneCount)
        {
            return Error.Validation(
                code: "Device.ZoneCountExceeded",
                description: $"Device '{request.DeviceId}' supports up to {layoutContext.MaxZoneCount} zones.");
        }

        if (request.Zones.Any(zone => zone.ZoneIndex > layoutContext.MaxZoneCount))
        {
            return Error.Validation(
                code: "Device.ZoneIndexOutOfRange",
                description: $"Zone indexes must be less than or equal to {layoutContext.MaxZoneCount} for device '{request.DeviceId}'.");
        }

        var resolvedTemplateId = request.TemplateId;
        var templateSource = string.IsNullOrWhiteSpace(request.TemplateId) ? "Inherited" : "Request";

        if (string.IsNullOrWhiteSpace(request.TemplateId))
        {
            LocationTemplateContextSnapshot templateContext = await this.locationTemplateContextRunner
                .ResolveTemplateContextAsync(request.LocationNodeId, cancellationToken)
                .ConfigureAwait(false);

            resolvedTemplateId = templateContext.ResolvedTemplateId;
            templateSource = templateContext.TemplateSource;
        }

        var response = new PreviewDeviceAssignmentResponse
        {
            DeviceId = request.DeviceId,
            LocationNodeId = request.LocationNodeId,
            ResolvedTemplateId = resolvedTemplateId,
            TemplateSource = templateSource,
            ZoneCount = request.Zones.Count,
            DuplicateProductsAllowed = true,
        };

        return response;
    }
}
