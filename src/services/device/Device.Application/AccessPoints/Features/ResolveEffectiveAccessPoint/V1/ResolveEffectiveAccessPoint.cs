// <copyright file="ResolveEffectiveAccessPoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.AccessPoints;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;
using SharedKernel.Events;
using Wolverine;
using DomainAccessPointReadRepository = Device.Domain.AccessPoints.IAccessPointReadRepository;
using DomainAccessPointWriteRepository = Device.Domain.AccessPoints.IAccessPointWriteRepository;

namespace Device.Application.AccessPoints.Features.ResolveEffectiveAccessPoint.V1;

/// <summary>
/// Query to resolve the nearest available access point for a display location and vendor.
/// </summary>
/// <param name="DisplayLocationNodeId">Display location node identifier.</param>
/// <param name="Vendor">Vendor name.</param>
/// <param name="ParentLocationNodeIds">Parent chain ordered from nearest parent to root.</param>
public sealed record ResolveEffectiveAccessPointQuery(
    string DisplayLocationNodeId,
    string Vendor,
    IReadOnlyList<string> ParentLocationNodeIds)
    : IQuery<ErrorOr<ResolveEffectiveAccessPointResponse>>;

/// <summary>
/// Response from resolving an effective access point.
/// </summary>
/// <param name="AccessPointId">Resolved access point identifier.</param>
/// <param name="SerialNumber">Supplier serial number.</param>
/// <param name="Vendor">Vendor name.</param>
/// <param name="LocationNodeId">Location where the access point was found.</param>
/// <param name="CurrentLoad">Current assigned display count after reservation.</param>
/// <param name="MaxCapacity">Maximum supported display count.</param>
public sealed record ResolveEffectiveAccessPointResponse(
    Guid AccessPointId,
    string SerialNumber,
    string Vendor,
    string LocationNodeId,
    int CurrentLoad,
    int MaxCapacity);

/// <summary>
/// Handler for <see cref="ResolveEffectiveAccessPointQuery"/>.
/// </summary>
internal sealed class ResolveEffectiveAccessPointQueryHandler(
    DomainAccessPointReadRepository readRepository,
    DomainAccessPointWriteRepository writeRepository,
    IUnitOfWork unitOfWork,
    IMessageBus messageBus)
    : IQueryHandler<ResolveEffectiveAccessPointQuery, ErrorOr<ResolveEffectiveAccessPointResponse>>
{
    private readonly DomainAccessPointReadRepository readRepository = readRepository;
    private readonly DomainAccessPointWriteRepository writeRepository = writeRepository;
    private readonly IUnitOfWork unitOfWork = unitOfWork;
    private readonly IMessageBus messageBus = messageBus;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<ResolveEffectiveAccessPointResponse>> Handle(
        ResolveEffectiveAccessPointQuery request,
        CancellationToken cancellationToken)
    {
        foreach (string locationNodeId in GetLocationChain(request))
        {
            AccessPoint? accessPoint = await this.readRepository
                .FindByVendorAndLocationAsync(request.Vendor, locationNodeId, cancellationToken)
                .ConfigureAwait(false);

            if (accessPoint is null || accessPoint.Status != AccessPointStatus.Online)
            {
                continue;
            }

            if (accessPoint.CurrentLoad >= accessPoint.MaxCapacity)
            {
                continue;
            }

            int previousLoad = accessPoint.CurrentLoad;
            ErrorOr<Success> incrementResult = accessPoint.IncrementLoad();
            if (incrementResult.IsError)
            {
                continue;
            }

            await this.writeRepository.UpdateAsync(accessPoint, cancellationToken).ConfigureAwait(false);
            await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await this.messageBus
                .PublishAsync(new AccessPointLoadChangedIntegrationEvent(
                    accessPoint.Id,
                    accessPoint.SerialNumber,
                    accessPoint.LocationNodeId,
                    previousLoad,
                    accessPoint.CurrentLoad,
                    accessPoint.MaxCapacity,
                    DateTimeOffset.UtcNow))
                .ConfigureAwait(false);

            return new ResolveEffectiveAccessPointResponse(
                accessPoint.Id,
                accessPoint.SerialNumber,
                accessPoint.Vendor,
                accessPoint.LocationNodeId,
                accessPoint.CurrentLoad,
                accessPoint.MaxCapacity);
        }

        return Error.NotFound(
            "AccessPoint.EffectiveAccessPointNotFound",
            $"No online access point with capacity was found for vendor '{request.Vendor}'.");
    }

    private static IEnumerable<string> GetLocationChain(ResolveEffectiveAccessPointQuery request)
    {
        yield return request.DisplayLocationNodeId;

        foreach (string parentLocationNodeId in request.ParentLocationNodeIds)
        {
            yield return parentLocationNodeId;
        }
    }
}
