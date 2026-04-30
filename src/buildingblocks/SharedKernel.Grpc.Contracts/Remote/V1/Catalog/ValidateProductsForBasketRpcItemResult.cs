// <copyright file="ValidateProductsForBasketRpcItemResult.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Catalog;

/// <summary>
/// Validation result for one basket line item.
/// </summary>
public sealed record ValidateProductsForBasketRpcItemResult
{
    /// <summary>
    /// Gets or sets the product identifier that was validated.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this line is valid for checkout.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the resolved unit price when available.
    /// </summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the resolved currency code when available.
    /// </summary>
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// Gets or sets the failure code when validation fails.
    /// </summary>
    public string? FailureCode { get; set; }
}
