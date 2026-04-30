// <copyright file="BasketSnapshotClient.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using ErrorOr;
using Order.Application.Common.Interfaces;

namespace Order.Infrastructure.Basket;

/// <summary>
/// HTTP client wrapper for basket snapshot retrieval.
/// </summary>
public sealed class BasketSnapshotClient(HttpClient httpClient) : IBasketSnapshotClient
{
    private readonly HttpClient httpClient = httpClient;

    /// <inheritdoc/>
    public async Task<ErrorOr<BasketSnapshot>> GetByIdAsync(
        Guid basketId,
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        string path = $"/basket/v1/Basket/{basketId:D}?tenantId={tenantId:D}&customerId={customerId:D}";
        Uri requestUri = new(path, UriKind.Relative);

        HttpResponseMessage response = await this.httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Error.NotFound("Order.Basket.NotFound", $"Basket '{basketId}' was not found");
        }

        if (!response.IsSuccessStatusCode)
        {
            return Error.Unexpected(
                "Order.Basket.TransportFailure",
                $"Basket lookup failed with status {(int)response.StatusCode}");
        }

        BasketApiResponse? responseBody = await response.Content
            .ReadFromJsonAsync<BasketApiResponse>(cancellationToken)
            .ConfigureAwait(false);

        if (responseBody is null)
        {
            return Error.Unexpected("Order.Basket.EmptyResponse", "Basket service returned an empty response body");
        }

        List<BasketSnapshotLine> lines = (responseBody.Lines ?? [])
            .Select(line => new BasketSnapshotLine(line.ProductId, line.Quantity, line.UnitPrice, line.CurrencyCode))
            .ToList();

        return new BasketSnapshot(
            responseBody.BasketId,
            responseBody.TenantId,
            responseBody.CustomerId,
            responseBody.CurrencyCode ?? string.Empty,
            lines);
    }

    private sealed record BasketApiResponse(
        Guid BasketId,
        Guid TenantId,
        Guid CustomerId,
        string? CurrencyCode,
        IList<BasketApiLineResponse>? Lines);

    private sealed record BasketApiLineResponse(
        Guid ProductId,
        int Quantity,
        decimal UnitPrice,
        string CurrencyCode);
}
