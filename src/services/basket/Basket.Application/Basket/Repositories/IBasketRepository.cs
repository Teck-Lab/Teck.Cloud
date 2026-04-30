// <copyright file="IBasketRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Basket.Domain.Entities.BasketAggregate;

namespace Basket.Application.Basket.Repositories;

/// <summary>
/// Repository for mutable basket draft data.
/// </summary>
public interface IBasketRepository
{
    /// <summary>
    /// Gets basket by tenant and customer identifiers.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="customerId">Customer identifier.</param>
    /// <param name="isSignedIn">Whether the basket owner is authenticated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Basket draft when found; otherwise null.</returns>
    Task<BasketDraft?> GetByTenantAndCustomerAsync(
        Guid tenantId,
        Guid customerId,
        bool isSignedIn,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets basket by identifier and owner scope.
    /// </summary>
    /// <param name="basketId">Basket identifier.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="customerId">Customer identifier.</param>
    /// <param name="isSignedIn">Whether the basket owner is authenticated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Basket draft when found; otherwise null.</returns>
    Task<BasketDraft?> GetByIdAsync(
        Guid basketId,
        Guid tenantId,
        Guid customerId,
        bool isSignedIn,
        CancellationToken cancellationToken);

    /// <summary>
    /// Persists a basket draft.
    /// </summary>
    /// <param name="basket">Basket draft.</param>
    /// <param name="isSignedIn">Whether the basket owner is authenticated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(BasketDraft basket, bool isSignedIn, CancellationToken cancellationToken);
}
