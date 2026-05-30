// <copyright file="GetLocationTemplateContext.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Location.Application.Service.Abstractions;
using SharedKernel.Core.CQRS;

namespace Location.Application.Service.Features.GetLocationTemplateContext.V1;

public sealed record GetLocationTemplateContextQuery(string LocationNodeId)
    : IQuery<ErrorOr<GetLocationTemplateContextResponse>>;

public sealed class GetLocationTemplateContextQueryHandler(ILocationNodeReadRepository locationNodeReadRepository)
    : IQueryHandler<GetLocationTemplateContextQuery, ErrorOr<GetLocationTemplateContextResponse>>
{
    private const int MaxAncestorDepth = 32;

    private readonly ILocationNodeReadRepository locationNodeReadRepository = locationNodeReadRepository;

    public async ValueTask<ErrorOr<GetLocationTemplateContextResponse>> Handle(
        GetLocationTemplateContextQuery request,
        CancellationToken cancellationToken)
    {
        HashSet<string> visitedNodeIds = new(StringComparer.Ordinal);
        var ancestorDepthScanned = 0;
        string? currentNodeId = request.LocationNodeId;

        while (!string.IsNullOrWhiteSpace(currentNodeId))
        {
            if (!visitedNodeIds.Add(currentNodeId))
            {
                break;
            }

            LocationNodeSnapshot? node = await this.locationNodeReadRepository.GetByIdAsync(currentNodeId, cancellationToken).ConfigureAwait(false);
            if (node is null)
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(node.TemplateId))
            {
                return new GetLocationTemplateContextResponse
                {
                    LocationNodeId = request.LocationNodeId,
                    ResolvedTemplateId = node.TemplateId,
                    TemplateSource = ancestorDepthScanned == 0 ? "Location" : "Ancestor",
                    AncestorDepthScanned = ancestorDepthScanned,
                };
            }

            if (string.IsNullOrWhiteSpace(node.ParentLocationNodeId) || ancestorDepthScanned >= MaxAncestorDepth)
            {
                break;
            }

            ancestorDepthScanned++;
            currentNodeId = node.ParentLocationNodeId;
        }

        return new GetLocationTemplateContextResponse
        {
            LocationNodeId = request.LocationNodeId,
            ResolvedTemplateId = null,
            TemplateSource = "None",
            AncestorDepthScanned = ancestorDepthScanned,
        };
    }
}
