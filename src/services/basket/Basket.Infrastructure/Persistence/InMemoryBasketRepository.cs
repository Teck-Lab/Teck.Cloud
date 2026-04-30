// <copyright file="InMemoryBasketRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Basket.Application.Basket.Repositories;
using Basket.Domain.Entities.BasketAggregate;

namespace Basket.Infrastructure.Persistence;

/// <summary>
/// In-memory basket repository for initial service scaffold.
/// </summary>
public sealed class InMemoryBasketRepository : IBasketRepository
{
    private readonly ConcurrentDictionary<string, BasketDraft> store = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task<BasketDraft?> GetByTenantAndCustomerAsync(
        Guid tenantId,
        Guid customerId,
        bool isSignedIn,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = isSignedIn;
        string key = BuildKey(tenantId, customerId);
        this.store.TryGetValue(key, out BasketDraft? basket);
        return Task.FromResult(basket);
    }

    /// <inheritdoc/>
    public Task<BasketDraft?> GetByIdAsync(
        Guid basketId,
        Guid tenantId,
        Guid customerId,
        bool isSignedIn,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = isSignedIn;

        BasketDraft? basket = this.store.Values.FirstOrDefault(candidate =>
            candidate.Id == basketId &&
            candidate.TenantId == tenantId &&
            candidate.CustomerId == customerId);

        return Task.FromResult(basket);
    }

    /// <inheritdoc/>
    public Task SaveAsync(BasketDraft basket, bool isSignedIn, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = isSignedIn;
        string key = BuildKey(basket.TenantId, basket.CustomerId);
        this.store[key] = basket;
        return Task.CompletedTask;
    }

    private static string BuildKey(Guid tenantId, Guid customerId)
    {
        return $"{tenantId:D}:{customerId:D}";
    }
}
