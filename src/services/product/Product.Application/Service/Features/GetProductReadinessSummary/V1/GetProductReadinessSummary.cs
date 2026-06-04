// <copyright file="GetProductReadinessSummary.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Product.Application.Service.Features.GetProductReadinessSummary.V1;

/// <summary>
/// Represents a query for retrieving product readiness summary information.
/// </summary>
public sealed record GetProductReadinessSummaryQuery : IQuery<ErrorOr<GetProductReadinessSummaryResponse>>;

/// <summary>
/// Handles <see cref="GetProductReadinessSummaryQuery"/> requests.
/// </summary>
public sealed class GetProductReadinessSummaryQueryHandler
    : IQueryHandler<GetProductReadinessSummaryQuery, ErrorOr<GetProductReadinessSummaryResponse>>
{
    /// <summary>
    /// Handles a request to retrieve the product readiness summary.
    /// </summary>
    /// <param name="request">The product readiness summary query request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A value task containing either an error or the product readiness summary response.
    /// </returns>
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
