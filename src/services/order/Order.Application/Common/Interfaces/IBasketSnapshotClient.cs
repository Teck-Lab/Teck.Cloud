// <copyright file="IBasketSnapshotClient.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;

namespace Order.Application.Common.Interfaces;

/// <summary>
/// Client for reading basket snapshots from Basket service.
/// </summary>
public interface IBasketSnapshotClient
{
    /// <summary>
    /// Gets a basket snapshot by identifier.
    /// </summary>
    /// <param name="basketId">Basket identifier.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="customerId">Customer identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Basket snapshot or transport/validation errors.</returns>
    Task<ErrorOr<BasketSnapshot>> GetByIdAsync(
        Guid basketId,
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Basket snapshot for order creation.
/// </summary>
/// <param name="BasketId">Basket identifier.</param>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="CustomerId">Customer identifier.</param>
/// <param name="CurrencyCode">Basket currency code.</param>
/// <param name="Lines">Basket line snapshots.</param>
public sealed record BasketSnapshot(
    Guid BasketId,
    Guid TenantId,
    Guid CustomerId,
    string CurrencyCode,
    IReadOnlyList<BasketSnapshotLine> Lines);

/// <summary>
/// Basket line snapshot.
/// </summary>
/// <param name="ProductId">Product identifier.</param>
/// <param name="Quantity">Line quantity.</param>
/// <param name="UnitPrice">Line unit price.</param>
/// <param name="CurrencyCode">Line currency code.</param>
public sealed record BasketSnapshotLine(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    string CurrencyCode);
