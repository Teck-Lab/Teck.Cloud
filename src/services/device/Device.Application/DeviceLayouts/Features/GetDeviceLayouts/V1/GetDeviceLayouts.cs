// <copyright file="GetDeviceLayouts.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceLayouts.Abstractions;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Device.Application.DeviceLayouts.Features.GetDeviceLayouts.V1;

/// <summary>
/// Query to list all layouts for a given device definition.
/// </summary>
/// <param name="DeviceDefinitionId">The device definition identifier to filter by.</param>
public sealed record GetDeviceLayoutsQuery(Guid DeviceDefinitionId)
    : IQuery<ErrorOr<IReadOnlyList<GetDeviceLayoutItemResponse>>>;

/// <summary>
/// Handler for <see cref="GetDeviceLayoutsQuery"/>.
/// </summary>
internal sealed class GetDeviceLayoutsQueryHandler(IDeviceLayoutReadRepository repository)
    : IQueryHandler<GetDeviceLayoutsQuery, ErrorOr<IReadOnlyList<GetDeviceLayoutItemResponse>>>
{
    private readonly IDeviceLayoutReadRepository repository = repository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<IReadOnlyList<GetDeviceLayoutItemResponse>>> Handle(
        GetDeviceLayoutsQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DeviceLayoutSnapshot> snapshots = await this.repository
            .GetByDeviceDefinitionIdAsync(request.DeviceDefinitionId, cancellationToken)
            .ConfigureAwait(false);

        List<GetDeviceLayoutItemResponse> items = snapshots
            .Select(snapshot => new GetDeviceLayoutItemResponse(
                snapshot.Id,
                snapshot.DeviceDefinitionId,
                snapshot.Name,
                snapshot.MaxZoneCount))
            .ToList();

        return items;
    }
}
