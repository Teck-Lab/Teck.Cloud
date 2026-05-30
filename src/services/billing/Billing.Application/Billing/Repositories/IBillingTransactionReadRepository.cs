// <copyright file="IBillingTransactionReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Billing.Application.Billing.ReadModels;
using SharedKernel.Core.Database;
using SharedKernel.Core.Pagination;

namespace Billing.Application.Billing.Repositories;

/// <summary>
/// Repository interface for read operations on BillingTransaction entities.
/// </summary>
public interface IBillingTransactionReadRepository : IGenericReadRepository<BillingTransactionReadModel, Guid>
{
    /// <summary>
    /// Gets a paged list of billing transactions with optional filters.
    /// </summary>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="size">The page size.</param>
    /// <param name="tenantId">Optional tenant identifier filter.</param>
    /// <param name="status">Optional status name filter.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A paged list of billing transaction read models.</returns>
    Task<PagedList<BillingTransactionReadModel>> GetPagedTransactionsAsync(
        int page,
        int size,
        Guid? tenantId,
        string? status,
        CancellationToken ct = default);
}
