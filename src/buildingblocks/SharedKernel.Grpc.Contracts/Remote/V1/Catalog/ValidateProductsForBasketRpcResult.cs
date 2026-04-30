// <copyright file="ValidateProductsForBasketRpcResult.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Catalog;

/// <summary>
/// Catalog basket validation RPC response payload.
/// </summary>
public sealed class ValidateProductsForBasketRpcResult
{
    /// <summary>
    /// Gets or sets a value indicating whether validation completed successfully.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets an error detail message when <see cref="Succeeded"/> is false.
    /// </summary>
    public string? ErrorDetail { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when validation was generated.
    /// </summary>
    public DateTimeOffset ValidatedAtUtc { get; set; }

    /// <summary>
    /// Gets validation results for each requested line item.
    /// </summary>
    public IList<ValidateProductsForBasketRpcItemResult> Items { get; } = [];
}
