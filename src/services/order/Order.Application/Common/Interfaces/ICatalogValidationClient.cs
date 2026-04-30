// <copyright file="ICatalogValidationClient.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;

namespace Order.Application.Common.Interfaces;

/// <summary>
/// Validates order line candidates against Catalog service.
/// </summary>
public interface ICatalogValidationClient
{
    /// <summary>
    /// Validates product/quantity lines with catalog.
    /// </summary>
    /// <param name="items">Line items to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result or transport errors.</returns>
    Task<ErrorOr<CatalogValidationResult>> ValidateAsync(
        IReadOnlyCollection<CatalogValidationItemRequest> items,
        CancellationToken cancellationToken);
}

/// <summary>
/// Catalog validation line request.
/// </summary>
/// <param name="ProductId">Product identifier.</param>
/// <param name="Quantity">Requested quantity.</param>
public sealed record CatalogValidationItemRequest(Guid ProductId, int Quantity);

/// <summary>
/// Catalog validation aggregate result.
/// </summary>
/// <param name="Items">Validation line results.</param>
public sealed record CatalogValidationResult(IReadOnlyList<CatalogValidationItemResult> Items);

/// <summary>
/// Catalog validation line result.
/// </summary>
/// <param name="ProductId">Product identifier.</param>
/// <param name="IsValid">Whether line is valid.</param>
/// <param name="UnitPrice">Resolved unit price when valid.</param>
/// <param name="CurrencyCode">Resolved currency code when valid.</param>
/// <param name="FailureCode">Failure code when invalid.</param>
public sealed record CatalogValidationItemResult(
    Guid ProductId,
    bool IsValid,
    decimal? UnitPrice,
    string? CurrencyCode,
    string? FailureCode);
