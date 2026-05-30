// <copyright file="GetPaginatedBillingTransactions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Billing.Application.Billing.ReadModels;
using Billing.Application.Billing.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Pagination;

namespace Billing.Application.Billing.Features.GetPaginatedBillingTransactions.V1;

/// <summary>
/// Query for paginated billing transactions.
/// </summary>
/// <param name="Page">Page number (1-based).</param>
/// <param name="Size">Page size.</param>
/// <param name="TenantId">Optional tenant identifier filter.</param>
/// <param name="Status">Optional status name filter.</param>
public sealed record GetPaginatedBillingTransactionsQuery(int Page, int Size, Guid? TenantId, string? Status)
    : IQuery<ErrorOr<PagedList<GetPaginatedBillingTransactionsResponse>>>;

/// <summary>
/// Handler for <see cref="GetPaginatedBillingTransactionsQuery"/>.
/// </summary>
public sealed class GetPaginatedBillingTransactionsQueryHandler(
    IBillingTransactionReadRepository billingTransactionReadRepository)
    : IQueryHandler<GetPaginatedBillingTransactionsQuery, ErrorOr<PagedList<GetPaginatedBillingTransactionsResponse>>>
{
    private readonly IBillingTransactionReadRepository billingTransactionReadRepository = billingTransactionReadRepository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<PagedList<GetPaginatedBillingTransactionsResponse>>> Handle(
        GetPaginatedBillingTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        PagedList<BillingTransactionReadModel> transactions = await this.billingTransactionReadRepository
            .GetPagedTransactionsAsync(request.Page, request.Size, request.TenantId, request.Status, cancellationToken)
            .ConfigureAwait(false);

        IList<GetPaginatedBillingTransactionsResponse> items = transactions.Items
            .Select(tx => new GetPaginatedBillingTransactionsResponse
            {
                Id = tx.Id,
                TenantId = tx.TenantId,
                CorrelationId = tx.CorrelationId,
                Amount = tx.Amount,
                Currency = tx.Currency,
                PaymentMethodId = tx.PaymentMethodId,
                ExternalChargeId = tx.ExternalChargeId,
                StatusName = tx.StatusName,
                Description = tx.Description,
                CreatedAt = tx.CreatedAt,
                UpdatedAt = tx.UpdatedAt,
            })
            .ToList();

        return new PagedList<GetPaginatedBillingTransactionsResponse>(
            items,
            transactions.TotalItems,
            transactions.Page,
            transactions.Size);
    }
}
