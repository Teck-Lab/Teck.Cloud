// <copyright file="InMemoryOrderRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Order.Application.Orders.Repositories;
using Order.Domain.Entities.OrderAggregate;

namespace Order.Infrastructure.Persistence;

/// <summary>
/// In-memory order repository for initial scaffold.
/// </summary>
public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<Guid, OrderDraft> store = [];

    /// <inheritdoc/>
    public Task SaveAsync(OrderDraft order, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.store[order.Id] = order;
        return Task.CompletedTask;
    }
}
