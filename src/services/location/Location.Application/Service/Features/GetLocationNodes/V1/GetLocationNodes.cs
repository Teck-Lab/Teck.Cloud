// <copyright file="GetLocationNodes.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Location.Application.Service.Abstractions;
using SharedKernel.Core.CQRS;

namespace Location.Application.Service.Features.GetLocationNodes.V1;

/// <summary>
/// Response item for a single location node.
/// </summary>
/// <param name="LocationNodeId">Unique location node identifier.</param>
/// <param name="Name">Human-readable name (null if not set).</param>
/// <param name="ParentLocationNodeId">Parent node identifier (null if root).</param>
public sealed record GetLocationNodeItemResponse(
    string LocationNodeId,
    string? Name,
    string? ParentLocationNodeId);

/// <summary>
/// Query for searching location nodes by name.
/// </summary>
/// <param name="Query">Optional search term; returns all nodes when omitted.</param>
public sealed record GetLocationNodesQuery(string? Query)
    : IQuery<ErrorOr<IReadOnlyList<GetLocationNodeItemResponse>>>;

/// <summary>
/// Handler for <see cref="GetLocationNodesQuery"/>.
/// </summary>
internal sealed class GetLocationNodesQueryHandler(ILocationNodeReadRepository repository)
    : IQueryHandler<GetLocationNodesQuery, ErrorOr<IReadOnlyList<GetLocationNodeItemResponse>>>
{
    private readonly ILocationNodeReadRepository repository = repository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<IReadOnlyList<GetLocationNodeItemResponse>>> Handle(
        GetLocationNodesQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LocationNodeSnapshot> snapshots = await this.repository
            .SearchByNameAsync(request.Query, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<GetLocationNodeItemResponse> items = snapshots
            .Select(snapshot => new GetLocationNodeItemResponse(snapshot.LocationNodeId, snapshot.Name, snapshot.ParentLocationNodeId))
            .ToList();

        return ErrorOrFactory.From(items);
    }
}
