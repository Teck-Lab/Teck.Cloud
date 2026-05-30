// <copyright file="GetProductReadinessSummary.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Product.Application.Service.Features.GetProductReadinessSummary.V1;

public sealed record GetProductReadinessSummaryQuery : IQuery<ErrorOr<GetProductReadinessSummaryResponse>>;

public sealed class GetProductReadinessSummaryQueryHandler
    : IQueryHandler<GetProductReadinessSummaryQuery, ErrorOr<GetProductReadinessSummaryResponse>>
{
    public ValueTask<ErrorOr<GetProductReadinessSummaryResponse>> Handle(
        GetProductReadinessSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var response = new GetProductReadinessSummaryResponse
        {
            ProductCount = 0,
            RenderReadyProductCount = 0,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
        };

        return ValueTask.FromResult<ErrorOr<GetProductReadinessSummaryResponse>>(response);
    }
}
