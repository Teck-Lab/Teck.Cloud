// <copyright file="ICatalogValidationClient.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;

namespace Basket.Application.Common.Interfaces;

/// <summary>
/// Validates basket product lines against Catalog service data.
/// </summary>
public interface ICatalogValidationClient
{
    /// <summary>
    /// Validates line items with the catalog service.
    /// </summary>
    /// <param name="items">Line items to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result or transport errors.</returns>
    Task<ErrorOr<CatalogValidationResult>> ValidateAsync(
        IReadOnlyCollection<CatalogValidationItemRequest> items,
        CancellationToken cancellationToken);
}

/// <summary>
/// Catalog validation item request.
/// </summary>
/// <param name="ProductId">Product identifier.</param>
/// <param name="Quantity">Requested quantity.</param>
public sealed record CatalogValidationItemRequest(Guid ProductId, int Quantity);

/// <summary>
/// Catalog validation aggregate result.
/// </summary>
/// <param name="Items">Validated items.</param>
public sealed record CatalogValidationResult(IReadOnlyList<CatalogValidationItemResult> Items);

/// <summary>
/// Catalog validation result for a single item.
/// </summary>
/// <param name="ProductId">Product identifier.</param>
/// <param name="IsValid">Whether catalog allows checkout for this line.</param>
/// <param name="UnitPrice">Resolved unit price when valid.</param>
/// <param name="CurrencyCode">Resolved currency code.</param>
/// <param name="FailureCode">Failure code when invalid.</param>
public sealed record CatalogValidationItemResult(
    Guid ProductId,
    bool IsValid,
    decimal? UnitPrice,
    string? CurrencyCode,
    string? FailureCode);
