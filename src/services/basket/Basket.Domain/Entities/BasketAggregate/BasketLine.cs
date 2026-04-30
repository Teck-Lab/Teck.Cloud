// <copyright file="BasketLine.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Basket.Domain.Entities.BasketAggregate;

/// <summary>
/// Represents one product line in a basket.
/// </summary>
public sealed class BasketLine
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BasketLine"/> class.
    /// </summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="quantity">Line quantity.</param>
    /// <param name="unitPrice">Current unit price.</param>
    /// <param name="currencyCode">Currency code.</param>
    public BasketLine(Guid productId, int quantity, decimal unitPrice, string currencyCode)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(quantity, 0);

        this.ProductId = productId;
        this.Quantity = quantity;
        this.UnitPrice = unitPrice;
        this.CurrencyCode = currencyCode;
    }

    /// <summary>
    /// Gets the product identifier.
    /// </summary>
    public Guid ProductId { get; }

    /// <summary>
    /// Gets the line quantity.
    /// </summary>
    public int Quantity { get; private set; }

    /// <summary>
    /// Gets the current unit price for this line.
    /// </summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>
    /// Gets the currency code.
    /// </summary>
    public string CurrencyCode { get; private set; }

    /// <summary>
    /// Gets the line total.
    /// </summary>
    public decimal LineTotal => this.UnitPrice * this.Quantity;

    /// <summary>
    /// Increases quantity and refreshes catalog price snapshot.
    /// </summary>
    /// <param name="quantity">Quantity to add.</param>
    /// <param name="unitPrice">Latest unit price.</param>
    /// <param name="currencyCode">Latest currency code.</param>
    public void Increase(int quantity, decimal unitPrice, string currencyCode)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(quantity, 0);

        this.Quantity += quantity;
        this.UnitPrice = unitPrice;
        this.CurrencyCode = currencyCode;
    }
}
