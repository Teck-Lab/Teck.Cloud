// <copyright file="GetDeviceDefinitions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.Abstractions;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Pagination;

namespace Device.Application.DeviceDefinitions.Features.GetDeviceDefinitions.V1;

/// <summary>
/// Query for a paginated list of device definitions.
/// </summary>
/// <param name="Page">Page number (1-based).</param>
/// <param name="Size">Page size.</param>
/// <param name="SortBy">Column to sort by: modelId, name, eslProvider.</param>
/// <param name="SortDescending">Sort direction; true for descending.</param>
public sealed record GetDeviceDefinitionsQuery(int Page, int Size, string? SortBy, bool SortDescending)
    : IQuery<ErrorOr<PagedList<GetDeviceDefinitionItemResponse>>>;

/// <summary>
/// Handler for <see cref="GetDeviceDefinitionsQuery"/>.
/// </summary>
internal sealed class GetDeviceDefinitionsQueryHandler(IDeviceDefinitionReadRepository repository)
    : IQueryHandler<GetDeviceDefinitionsQuery, ErrorOr<PagedList<GetDeviceDefinitionItemResponse>>>
{
    private readonly IDeviceDefinitionReadRepository repository = repository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<PagedList<GetDeviceDefinitionItemResponse>>> Handle(
        GetDeviceDefinitionsQuery request,
        CancellationToken cancellationToken)
    {
        PagedList<DeviceDefinitionSnapshot> paged = await this.repository
            .GetPagedAsync(request.Page, request.Size, request.SortBy, request.SortDescending, cancellationToken)
            .ConfigureAwait(false);

        List<GetDeviceDefinitionItemResponse> items = paged.Items
            .Select(snapshot => new GetDeviceDefinitionItemResponse(
                snapshot.Id,
                snapshot.ModelId,
                snapshot.Name,
                snapshot.WidthPx,
                snapshot.HeightPx,
                (int)snapshot.SupportedColors,
                snapshot.SupportsNfc,
                snapshot.EslProvider.Name,
                snapshot.CatalogManufacturerId,
                snapshot.CatalogSupplierId,
                snapshot.CatalogProductId))
            .ToList();

        return new PagedList<GetDeviceDefinitionItemResponse>(items, paged.TotalItems, paged.Page, paged.Size);
    }
}
