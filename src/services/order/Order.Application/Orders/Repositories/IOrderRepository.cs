// <copyright file="IOrderRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Order.Domain.Entities.OrderAggregate;

namespace Order.Application.Orders.Repositories;

/// <summary>
/// Repository abstraction for order drafts.
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Saves order draft.
    /// </summary>
    /// <param name="order">Order draft.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(OrderDraft order, CancellationToken cancellationToken);
}
