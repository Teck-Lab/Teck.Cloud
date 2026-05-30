// <copyright file="GetDisplays.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Displays.Abstractions;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Device.Application.Displays.Features.GetDisplays.V1;

/// <summary>
/// Query for displays belonging to a location node.
/// </summary>
/// <param name="LocationNodeId">Location node identifier.</param>
public sealed record GetDisplaysQuery(string LocationNodeId)
    : IQuery<ErrorOr<IReadOnlyList<GetDisplayItemResponse>>>;

/// <summary>
/// Handler for <see cref="GetDisplaysQuery"/>.
/// </summary>
internal sealed class GetDisplaysQueryHandler(IDisplayReadRepository repository)
    : IQueryHandler<GetDisplaysQuery, ErrorOr<IReadOnlyList<GetDisplayItemResponse>>>
{
    private readonly IDisplayReadRepository repository = repository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<IReadOnlyList<GetDisplayItemResponse>>> Handle(
        GetDisplaysQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DisplaySnapshot> snapshots = await this.repository
            .GetByLocationAsync(request.LocationNodeId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<GetDisplayItemResponse> items = snapshots
            .Select(snapshot => new GetDisplayItemResponse(
                snapshot.DisplayId,
                snapshot.ShortSerial,
                snapshot.LongSerial,
                snapshot.LocationNodeId,
                snapshot.DeviceDefinitionId,
                snapshot.CreatedAt))
            .ToList();

        return ErrorOrFactory.From(items);
    }
}
