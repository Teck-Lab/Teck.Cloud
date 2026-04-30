// <copyright file="ValidateProductsForBasketResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Products.Features.ValidateProductsForBasket.V1;

/// <summary>
/// Validation response for basket line items.
/// </summary>
public sealed record ValidateProductsForBasketResponse
{
    /// <summary>
    /// Gets or sets the UTC timestamp when the catalog validation was generated.
    /// </summary>
    public DateTimeOffset ValidatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets validation results for each requested line item.
    /// </summary>
    public IReadOnlyList<ValidateProductsForBasketItemResponse> Items { get; set; } = [];
}

/// <summary>
/// Validation response for one basket line item.
/// </summary>
public sealed record ValidateProductsForBasketItemResponse
{
    /// <summary>
    /// Gets or sets the product identifier that was validated.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Gets or sets the requested quantity.
    /// </summary>
    public int RequestedQuantity { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the product exists.
    /// </summary>
    public bool Exists { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the product is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the resolved unit price when available.
    /// </summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the currency code for the resolved unit price.
    /// </summary>
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether any active promotion currently applies.
    /// </summary>
    public bool HasActiveRebate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the requested quantity is within configured limits.
    /// </summary>
    public bool QuantityWithinLimits { get; set; }

    /// <summary>
    /// Gets or sets the minimum allowed quantity when configured.
    /// </summary>
    public int? MinQuantity { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed quantity when configured.
    /// </summary>
    public int? MaxQuantity { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this line is valid for checkout.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the failure code when validation fails.
    /// </summary>
    public string? FailureCode { get; set; }
}
