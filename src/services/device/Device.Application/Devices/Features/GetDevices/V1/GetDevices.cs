// <copyright file="GetDevices.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.Abstractions;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Pagination;

namespace Device.Application.Devices.Features.GetDevices.V1;

/// <summary>
/// Query for a paginated list of device definitions.
/// </summary>
/// <param name="Page">Page number (1-based).</param>
/// <param name="Size">Page size.</param>
/// <param name="SortBy">Column to sort by: deviceId, maxZoneCount, updatedAtUtc. Defaults to deviceId.</param>
/// <param name="SortDescending">Sort direction; true for descending.</param>
public sealed record GetDevicesQuery(int Page, int Size, string? SortBy, bool SortDescending)
    : IQuery<ErrorOr<PagedList<GetDeviceItemResponse>>>;

/// <summary>
/// Handler for <see cref="GetDevicesQuery"/>.
/// </summary>
internal sealed class GetDevicesQueryHandler(IDeviceDefinitionReadRepository repository)
    : IQueryHandler<GetDevicesQuery, ErrorOr<PagedList<GetDeviceItemResponse>>>
{
    private readonly IDeviceDefinitionReadRepository repository = repository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<PagedList<GetDeviceItemResponse>>> Handle(
        GetDevicesQuery request,
        CancellationToken cancellationToken)
    {
        PagedList<DeviceDefinitionSnapshot> paged = await this.repository
            .GetPagedAsync(request.Page, request.Size, request.SortBy, request.SortDescending, cancellationToken)
            .ConfigureAwait(false);

        List<GetDeviceItemResponse> items = paged.Items
            .Select(snapshot => new GetDeviceItemResponse(snapshot.Id, snapshot.ModelId, snapshot.Name, snapshot.EslProvider.Name))
            .ToList();

        return new PagedList<GetDeviceItemResponse>(items, paged.TotalItems, paged.Page, paged.Size);
    }
}
