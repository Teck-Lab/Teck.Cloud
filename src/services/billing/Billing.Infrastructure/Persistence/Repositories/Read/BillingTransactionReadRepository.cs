// <copyright file="BillingTransactionReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Billing.Application.Billing.ReadModels;
using Billing.Application.Billing.Repositories;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Pagination;
using SharedKernel.Persistence.Database.EFCore;

namespace Billing.Infrastructure.Persistence.Repositories.Read;

/// <summary>
/// Repository for read operations on BillingTransaction entities.
/// </summary>
public sealed class BillingTransactionReadRepository : GenericReadRepository<BillingTransactionReadModel, Guid, BillingReadDbContext>, IBillingTransactionReadRepository
{
    private readonly DbSet<BillingTransactionReadModel> billingTransactions;

    /// <summary>
    /// Initializes a new instance of the <see cref="BillingTransactionReadRepository"/> class.
    /// </summary>
    /// <param name="readDbContext">The read database context.</param>
    public BillingTransactionReadRepository(BillingReadDbContext readDbContext)
        : base(readDbContext)
    {
        this.billingTransactions = this.DbContext.BillingTransactions;
    }

    /// <inheritdoc/>
    public async Task<PagedList<BillingTransactionReadModel>> GetPagedTransactionsAsync(
        int page,
        int size,
        Guid? tenantId,
        string? status,
        CancellationToken ct = default)
    {
        var query = this.billingTransactions.AsNoTracking().AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Where(transaction => transaction.TenantId == tenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(transaction => transaction.StatusName == status);
        }

        int totalCount = await query.CountAsync(ct).ConfigureAwait(false);
        List<BillingTransactionReadModel> items = await query
            .OrderByDescending(transaction => transaction.UpdatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PagedList<BillingTransactionReadModel>(items, totalCount, page, size);
    }
}
