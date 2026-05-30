// <copyright file="GetAccessPoints.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.AccessPoints;
using ErrorOr;
using SharedKernel.Core.CQRS;
using DomainAccessPointReadRepository = Device.Domain.AccessPoints.IAccessPointReadRepository;

namespace Device.Application.AccessPoints.Features.GetAccessPoints.V1;

/// <summary>
/// Query for access points belonging to a location node.
/// </summary>
/// <param name="LocationNodeId">Location node identifier.</param>
public sealed record GetAccessPointsQuery(string LocationNodeId)
    : IQuery<ErrorOr<IReadOnlyList<GetAccessPointItemResponse>>>;

/// <summary>
/// Response item for a single access point.
/// </summary>
/// <param name="AccessPointId">Primary key.</param>
/// <param name="SerialNumber">Supplier serial number.</param>
/// <param name="Vendor">Vendor name.</param>
/// <param name="LocationNodeId">Assigned location node.</param>
/// <param name="Status">Operational status.</param>
/// <param name="MaxCapacity">Maximum supported display count.</param>
/// <param name="CurrentLoad">Current assigned display count.</param>
public sealed record GetAccessPointItemResponse(
    Guid AccessPointId,
    string SerialNumber,
    string Vendor,
    string LocationNodeId,
    AccessPointStatus Status,
    int MaxCapacity,
    int CurrentLoad);

/// <summary>
/// Handler for <see cref="GetAccessPointsQuery"/>.
/// </summary>
internal sealed class GetAccessPointsQueryHandler(DomainAccessPointReadRepository repository)
    : IQueryHandler<GetAccessPointsQuery, ErrorOr<IReadOnlyList<GetAccessPointItemResponse>>>
{
    private readonly DomainAccessPointReadRepository repository = repository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<IReadOnlyList<GetAccessPointItemResponse>>> Handle(
        GetAccessPointsQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AccessPoint> accessPoints = await this.repository
            .GetByLocationAsync(request.LocationNodeId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<GetAccessPointItemResponse> items = accessPoints
            .Select(accessPoint => new GetAccessPointItemResponse(
                accessPoint.Id,
                accessPoint.SerialNumber,
                accessPoint.Vendor,
                accessPoint.LocationNodeId,
                accessPoint.Status,
                accessPoint.MaxCapacity,
                accessPoint.CurrentLoad))
            .ToList();

        return ErrorOrFactory.From(items);
    }
}
