// <copyright file="OrderDraft.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Domain;

namespace Order.Domain.Entities.OrderAggregate;

/// <summary>
/// Represents a draft order created from a basket snapshot.
/// </summary>
public sealed class OrderDraft : BaseEntity
{
    private readonly List<OrderLine> lines = [];

    private OrderDraft(Guid tenantId, Guid customerId, Guid basketId)
    {
        this.TenantId = tenantId;
        this.CustomerId = customerId;
        this.BasketId = basketId;
        this.Status = "Pending";
        this.CreatedAtUtc = DateTimeOffset.UtcNow;
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
    /// Gets source basket identifier.
    /// </summary>
    public Guid BasketId { get; }

    /// <summary>
    /// Gets order status.
    /// </summary>
    public string Status { get; private set; }

    /// <summary>
    /// Gets creation timestamp in UTC.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>
    /// Gets order lines.
    /// </summary>
    public IReadOnlyList<OrderLine> Lines => this.lines;

    /// <summary>
    /// Creates order draft from basket line snapshots.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="customerId">Customer identifier.</param>
    /// <param name="basketId">Basket identifier.</param>
    /// <param name="lines">Order lines.</param>
    /// <returns>Created order draft.</returns>
    public static OrderDraft Create(
        Guid tenantId,
        Guid customerId,
        Guid basketId,
        IReadOnlyCollection<OrderLine> lines)
    {
        OrderDraft order = new(tenantId, customerId, basketId);
        order.lines.AddRange(lines);
        return order;
    }
}
