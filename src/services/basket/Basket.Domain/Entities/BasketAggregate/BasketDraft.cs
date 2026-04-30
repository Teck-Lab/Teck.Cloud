// <copyright file="BasketDraft.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Domain;

namespace Basket.Domain.Entities.BasketAggregate;

/// <summary>
/// Represents a mutable basket draft for one tenant and customer.
/// </summary>
public sealed class BasketDraft : BaseEntity
{
    private readonly List<BasketLine> lines = [];

    private BasketDraft(Guid tenantId, Guid customerId)
    {
        this.TenantId = tenantId;
        this.CustomerId = customerId;
    }

    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public Guid TenantId { get; }

    /// <summary>
    /// Gets the customer identifier.
    /// </summary>
    public Guid CustomerId { get; }

    /// <summary>
    /// Gets line items in this basket.
    /// </summary>
    public IReadOnlyList<BasketLine> Lines => this.lines;

    /// <summary>
    /// Creates a new basket draft.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="customerId">Customer identifier.</param>
    /// <returns>A new basket draft instance.</returns>
    public static BasketDraft Create(Guid tenantId, Guid customerId)
    {
        return new BasketDraft(tenantId, customerId);
    }

    /// <summary>
    /// Rehydrates a basket draft from persisted state.
    /// </summary>
    /// <param name="basketId">Basket identifier.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="customerId">Customer identifier.</param>
    /// <param name="lines">Persisted basket lines.</param>
    /// <returns>A rehydrated basket draft.</returns>
    public static BasketDraft Rehydrate(
        Guid basketId,
        Guid tenantId,
        Guid customerId,
        IEnumerable<BasketLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        BasketDraft basket = new(tenantId, customerId)
        {
            Id = basketId,
        };

        foreach (BasketLine line in lines)
        {
            basket.lines.Add(new BasketLine(line.ProductId, line.Quantity, line.UnitPrice, line.CurrencyCode));
        }

        return basket;
    }

    /// <summary>
    /// Adds or updates a product line in the basket.
    /// </summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="quantity">Quantity to add.</param>
    /// <param name="unitPrice">Current unit price.</param>
    /// <param name="currencyCode">Currency code.</param>
    public void AddOrUpdateLine(Guid productId, int quantity, decimal unitPrice, string currencyCode)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(quantity, 0);

        BasketLine? existing = this.lines.FirstOrDefault(line => line.ProductId == productId);
        if (existing is null)
        {
            this.lines.Add(new BasketLine(productId, quantity, unitPrice, currencyCode));
            return;
        }

        existing.Increase(quantity, unitPrice, currencyCode);
    }
}
