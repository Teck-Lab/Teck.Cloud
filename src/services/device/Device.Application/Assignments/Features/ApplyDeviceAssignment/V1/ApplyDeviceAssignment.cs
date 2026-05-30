// <copyright file="ApplyDeviceAssignment.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;
using Device.Application.Displays.Abstractions;
using Device.Domain.Entities.DisplayAggregate;
using Device.Domain.Entities.DisplayAssignmentAggregate;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;

namespace Device.Application.Assignments.Features.ApplyDeviceAssignment.V1;

public sealed record ApplyDeviceAssignmentCommand(
    string DeviceId,
    string LocationNodeId,
    string? TemplateId,
    IReadOnlyList<ApplyDeviceAssignmentZoneRequest> Zones)
    : ICommand<ErrorOr<ApplyDeviceAssignmentResponse>>;

public sealed class ApplyDeviceAssignmentCommandHandler(
    IDeviceDefinitionReadRepository deviceDefinitionReadRepository,
    IDisplayWriteRepository displayWriteRepository,
    IDisplayAssignmentWriteRepository displayAssignmentWriteRepository,
    ILocationTemplateContextRunner locationTemplateContextRunner,
    IProductSnapshotRunner productSnapshotRunner,
    ILabelRenderJobRunner labelRenderJobRunner,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ApplyDeviceAssignmentCommand, ErrorOr<ApplyDeviceAssignmentResponse>>
{
    private readonly IDeviceDefinitionReadRepository deviceDefinitionReadRepository = deviceDefinitionReadRepository;
    private readonly IDisplayWriteRepository displayWriteRepository = displayWriteRepository;
    private readonly IDisplayAssignmentWriteRepository displayAssignmentWriteRepository = displayAssignmentWriteRepository;
    private readonly ILocationTemplateContextRunner locationTemplateContextRunner = locationTemplateContextRunner;
    private readonly IProductSnapshotRunner productSnapshotRunner = productSnapshotRunner;
    private readonly ILabelRenderJobRunner labelRenderJobRunner = labelRenderJobRunner;
    private readonly IUnitOfWork unitOfWork = unitOfWork;

    public async ValueTask<ErrorOr<ApplyDeviceAssignmentResponse>> Handle(
        ApplyDeviceAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.DeviceId, out Guid displayId))
        {
            return Error.Validation(
                code: "Device.InvalidDeviceIdFormat",
                description: $"Device '{request.DeviceId}' must be a GUID to enqueue render jobs.");
        }

        Display? display = await this.displayWriteRepository
            .FindByIdAsync(displayId, cancellationToken)
            .ConfigureAwait(false);

        if (display is null)
        {
            return Error.NotFound(
                code: "Device.DisplayNotFound",
                description: $"Display '{request.DeviceId}' was not found.");
        }

        DisplayLayoutContext? deviceDefinition = await this.deviceDefinitionReadRepository
            .GetLayoutContextByDisplayIdAsync(displayId, cancellationToken)
            .ConfigureAwait(false);

        if (deviceDefinition is null)
        {
            return Error.NotFound(
                code: "Device.LayoutNotFound",
                description: $"Display '{request.DeviceId}' does not have a layout assigned.");
        }

        if (request.Zones.Select(zone => zone.ZoneIndex).Distinct().Count() != request.Zones.Count)
        {
            return Error.Validation(
                code: "Device.DuplicateZoneIndex",
                description: "Each zone index must be unique.");
        }

        if (request.Zones.Count > deviceDefinition.MaxZoneCount)
        {
            return Error.Validation(
                code: "Device.ZoneCountExceeded",
                description: $"Device '{request.DeviceId}' supports up to {deviceDefinition.MaxZoneCount} zones.");
        }

        if (request.Zones.Any(zone => zone.ZoneIndex > deviceDefinition.MaxZoneCount))
        {
            return Error.Validation(
                code: "Device.ZoneIndexOutOfRange",
                description: $"Zone indexes must be less than or equal to {deviceDefinition.MaxZoneCount} for device '{request.DeviceId}'.");
        }

        List<(ApplyDeviceAssignmentZoneRequest Zone, Guid ProductId)> parsedZones = [];
        foreach (ApplyDeviceAssignmentZoneRequest zone in request.Zones)
        {
            if (!Guid.TryParse(zone.ProductId, out Guid parsedProductId))
            {
                return Error.Validation(
                    code: "Device.InvalidProductIdFormat",
                    description: $"Product '{zone.ProductId}' must be a GUID.");
            }

            parsedZones.Add((zone, parsedProductId));
        }

        Guid[] uniqueProductIds = parsedZones
            .Select(item => item.ProductId)
            .Distinct()
            .ToArray();

        IReadOnlyList<ProductSnapshotItem> snapshots = await this.productSnapshotRunner
            .GetSnapshotsAsync("product", uniqueProductIds, cancellationToken)
            .ConfigureAwait(false);

        HashSet<Guid> snapshotIds = snapshots
            .Select(item => item.ProductId)
            .ToHashSet();

        Guid[] missingProductIds = uniqueProductIds
            .Where(productId => !snapshotIds.Contains(productId))
            .ToArray();

        if (missingProductIds.Length > 0)
        {
            return Error.Validation(
                code: "Device.ProductsNotFound",
                description: $"Missing product snapshots for: {string.Join(", ", missingProductIds)}.");
        }

        string? resolvedTemplateId = request.TemplateId;
        string templateSource = string.IsNullOrWhiteSpace(request.TemplateId) ? "Inherited" : "Request";
        LocationTemplateContextSnapshot? templateContext = null;

        if (string.IsNullOrWhiteSpace(request.TemplateId))
        {
            templateContext = await this.locationTemplateContextRunner
                .ResolveTemplateContextAsync(request.LocationNodeId, cancellationToken)
                .ConfigureAwait(false);

            resolvedTemplateId = templateContext.ResolvedTemplateId;
            templateSource = templateContext.TemplateSource;
        }

        if (string.IsNullOrWhiteSpace(resolvedTemplateId))
        {
            return Error.Validation(
                code: "Device.TemplateNotResolved",
                description: "A template is required to enqueue label rendering.");
        }

        DisplayAssignmentZone[] assignmentZones = parsedZones
            .Select(item => new DisplayAssignmentZone(item.Zone.ZoneIndex, item.ProductId))
            .ToArray();

        string? templateSnapshot = templateContext?.ResolvedTemplateDesign is not null
            ? JsonSerializer.Serialize(templateContext.ResolvedTemplateDesign)
            : null;

        string? productDataSnapshot = snapshots.Count > 0
            ? JsonSerializer.Serialize(snapshots)
            : null;

        ErrorOr<DisplayAssignment> createdAssignment = DisplayAssignment.Create(
            displayId,
            request.LocationNodeId,
            resolvedTemplateId,
            templateSource,
            templateSnapshot,
            productDataSnapshot,
            assignmentZones);

        if (createdAssignment.IsError)
        {
            return createdAssignment.Errors;
        }

        DisplayAssignment assignment = createdAssignment.Value;

        ErrorOr<Success> assignResult = display.AssignCurrent(assignment.Id);
        if (assignResult.IsError)
        {
            return assignResult.Errors;
        }

        await this.displayAssignmentWriteRepository
            .AddAsync(assignment, cancellationToken)
            .ConfigureAwait(false);

        this.displayWriteRepository.Update(display);

        await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyCollection<LabelRenderJobZoneItem> renderZones = parsedZones
            .Select(item => new LabelRenderJobZoneItem(item.Zone.ZoneIndex, item.ProductId))
            .ToArray();

        LabelRenderJobResult renderJobResult = await this.labelRenderJobRunner
            .EnqueueAsync(assignment.RenderJobId, displayId, resolvedTemplateId, renderZones, templateContext?.ResolvedTemplateDesign, cancellationToken)
            .ConfigureAwait(false);

        if (renderJobResult.JobId == Guid.Empty)
        {
            return Error.Unexpected(
                code: "Device.RenderJobEnqueueFailed",
                description: "Label render job enqueue failed.");
        }

        return new ApplyDeviceAssignmentResponse
        {
            DeviceId = request.DeviceId,
            LocationNodeId = request.LocationNodeId,
            ResolvedTemplateId = resolvedTemplateId,
            TemplateSource = templateSource,
            ZoneCount = request.Zones.Count,
            DuplicateProductsAllowed = true,
            RenderJobId = renderJobResult.JobId,
            RenderJobStatus = renderJobResult.Status,
            AssignmentId = assignment.Id,
            AssignmentVersion = assignment.AssignmentVersion,
        };
    }
}
