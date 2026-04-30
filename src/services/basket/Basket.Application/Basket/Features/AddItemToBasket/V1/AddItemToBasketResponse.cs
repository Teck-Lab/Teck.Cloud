// <copyright file="AddItemToBasketResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Basket.Domain.Entities.BasketAggregate;

namespace Basket.Application.Basket.Features.AddItemToBasket.V1;

/// <summary>
/// Response for add item to basket operation.
/// </summary>
public sealed record AddItemToBasketResponse
{
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
    /// Gets or sets basket total quantity.
    /// </summary>
    public int TotalQuantity { get; set; }

    /// <summary>
    /// Gets or sets basket total amount.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets basket display currency.
    /// </summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets line snapshots.
    /// </summary>
    public IReadOnlyList<AddItemToBasketLineResponse> Lines { get; set; } = [];

    /// <summary>
    /// Maps domain basket draft to response.
    /// </summary>
    /// <param name="basket">Basket draft.</param>
    /// <returns>Mapped response.</returns>
    public static AddItemToBasketResponse FromDomain(BasketDraft basket)
    {
        string currency = basket.Lines.Count == 0
            ? string.Empty
            : basket.Lines[0].CurrencyCode;
        List<AddItemToBasketLineResponse> lines = basket.Lines
            .Select(line => new AddItemToBasketLineResponse
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                CurrencyCode = line.CurrencyCode,
                LineTotal = line.LineTotal,
            })
            .ToList();

        return new AddItemToBasketResponse
        {
            BasketId = basket.Id,
            TenantId = basket.TenantId,
            CustomerId = basket.CustomerId,
            TotalQuantity = lines.Sum(line => line.Quantity),
            TotalAmount = lines.Sum(line => line.LineTotal),
            CurrencyCode = currency,
            Lines = lines,
        };
    }
}

/// <summary>
/// Basket line response.
/// </summary>
public sealed record AddItemToBasketLineResponse
{
    /// <summary>
    /// Gets or sets product identifier.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Gets or sets line quantity.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets line unit price.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets line currency code.
    /// </summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets line total amount.
    /// </summary>
    public decimal LineTotal { get; set; }
}
