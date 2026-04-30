// <copyright file="CreateOrderFromBasketResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Order.Domain.Entities.OrderAggregate;

namespace Order.Application.Orders.Features.CreateOrderFromBasket.V1;

/// <summary>
/// Response for create order from basket operation.
/// </summary>
public sealed record CreateOrderFromBasketResponse
{
    /// <summary>
    /// Gets or sets order identifier.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Gets or sets basket identifier.
    /// </summary>
    public Guid BasketId { get; set; }

    /// <summary>
    /// Gets or sets tenant identifier.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Gets or sets customer identifier.
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Gets or sets order status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets total quantity.
    /// </summary>
    public int TotalQuantity { get; set; }

    /// <summary>
    /// Gets or sets total amount.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets order currency code.
    /// </summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets creation timestamp in UTC.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets order line snapshots.
    /// </summary>
    public IReadOnlyList<CreateOrderLineResponse> Lines { get; set; } = [];

    /// <summary>
    /// Maps domain order draft to API/application response.
    /// </summary>
    /// <param name="order">Order draft.</param>
    /// <returns>Mapped response.</returns>
    public static CreateOrderFromBasketResponse FromDomain(OrderDraft order)
    {
        List<CreateOrderLineResponse> lines = order.Lines
            .Select(line => new CreateOrderLineResponse
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                CurrencyCode = line.CurrencyCode,
                LineTotal = line.LineTotal,
            })
            .ToList();

        string currency = lines.Count == 0 ? string.Empty : lines[0].CurrencyCode;

        return new CreateOrderFromBasketResponse
        {
            OrderId = order.Id,
            BasketId = order.BasketId,
            TenantId = order.TenantId,
            CustomerId = order.CustomerId,
            Status = order.Status,
            TotalQuantity = lines.Sum(line => line.Quantity),
            TotalAmount = lines.Sum(line => line.LineTotal),
            CurrencyCode = currency,
            CreatedAtUtc = order.CreatedAtUtc,
            Lines = lines,
        };
    }
}

/// <summary>
/// Order line response.
/// </summary>
public sealed record CreateOrderLineResponse
{
    /// <summary>
    /// Gets or sets product identifier.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Gets or sets ordered quantity.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets unit price.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets currency code.
    /// </summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets line total.
    /// </summary>
    public decimal LineTotal { get; set; }
}
