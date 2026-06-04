// <copyright file="GetLocationNodeAncestors.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Location.Application.Service.Abstractions;
using SharedKernel.Core.CQRS;

namespace Location.Application.Service.Features.GetLocationNodeAncestors.V1;

/// <summary>
/// Query for retrieving ancestor node identifiers of a location node.
/// </summary>
/// <param name="LocationNodeId">The location node identifier.</param>
public sealed record GetLocationNodeAncestorsQuery(string LocationNodeId)
    : IQuery<ErrorOr<IReadOnlyList<string>>>;

internal sealed class GetLocationNodeAncestorsQueryHandler(ILocationNodeReadRepository locationNodeReadRepository)
    : IQueryHandler<GetLocationNodeAncestorsQuery, ErrorOr<IReadOnlyList<string>>>
{
    private const int MaxAncestorDepth = 32;

    private readonly ILocationNodeReadRepository locationNodeReadRepository = locationNodeReadRepository;

    public async ValueTask<ErrorOr<IReadOnlyList<string>>> Handle(
        GetLocationNodeAncestorsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LocationNodeId);

        HashSet<string> visitedNodeIds = new(StringComparer.Ordinal);
        List<string> ancestors = [];

        LocationNodeSnapshot? currentNode = await this.locationNodeReadRepository
            .GetByIdAsync(request.LocationNodeId, cancellationToken)
            .ConfigureAwait(false);

        if (currentNode is null)
        {
            return ErrorOrFactory.From<IReadOnlyList<string>>([]);
        }

        string? currentParentNodeId = currentNode.ParentLocationNodeId;
        int ancestorDepth = 0;

        while (!string.IsNullOrWhiteSpace(currentParentNodeId) && ancestorDepth < MaxAncestorDepth)
        {
            if (!visitedNodeIds.Add(currentParentNodeId))
            {
                break;
            }

            ancestors.Add(currentParentNodeId);

            LocationNodeSnapshot? parentNode = await this.locationNodeReadRepository
                .GetByIdAsync(currentParentNodeId, cancellationToken)
                .ConfigureAwait(false);

            if (parentNode is null)
            {
                break;
            }

            currentParentNodeId = parentNode.ParentLocationNodeId;
            ancestorDepth++;
        }

        return ErrorOrFactory.From<IReadOnlyList<string>>(ancestors);
    }
}
