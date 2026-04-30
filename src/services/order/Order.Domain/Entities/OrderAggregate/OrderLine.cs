// <copyright file="OrderLine.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Order.Domain.Entities.OrderAggregate;

/// <summary>
/// Represents one finalized order line snapshot.
/// </summary>
public sealed class OrderLine
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrderLine"/> class.
    /// </summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="quantity">Ordered quantity.</param>
    /// <param name="unitPrice">Unit price.</param>
    /// <param name="currencyCode">Currency code.</param>
    public OrderLine(Guid productId, int quantity, decimal unitPrice, string currencyCode)
    {
        this.ProductId = productId;
        this.Quantity = quantity;
        this.UnitPrice = unitPrice;
        this.CurrencyCode = currencyCode;
    }

    /// <summary>
    /// Gets product identifier.
    /// </summary>
    public Guid ProductId { get; }

    /// <summary>
    /// Gets ordered quantity.
    /// </summary>
    public int Quantity { get; }

    /// <summary>
    /// Gets unit price snapshot.
    /// </summary>
    public decimal UnitPrice { get; }

    /// <summary>
    /// Gets currency code.
    /// </summary>
    public string CurrencyCode { get; }

    /// <summary>
    /// Gets line total.
    /// </summary>
    public decimal LineTotal => this.UnitPrice * this.Quantity;
}
