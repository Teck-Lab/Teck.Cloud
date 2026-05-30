// <copyright file="GetPaginatedBillingTransactionsRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Pagination;

namespace Billing.Application.Billing.Features.GetPaginatedBillingTransactions.V1;

/// <summary>
/// Request model for paginated billing transaction queries.
/// </summary>
public sealed class GetPaginatedBillingTransactionsRequest : PaginationParameters
{
    /// <summary>
    /// Gets or sets the optional tenant identifier filter.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the optional status name filter (e.g., "Pending", "Succeeded", "Failed").
    /// </summary>
    public string? Status { get; set; }
}
