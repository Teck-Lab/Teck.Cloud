// <copyright file="CatalogValidationClient.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Basket.Application.Common.Interfaces;
using ErrorOr;
using FastEndpoints;
using Grpc.Core;
using SharedKernel.Grpc.Contracts.Remote.V1.Catalog;

namespace Basket.Infrastructure.Catalog;

/// <summary>
/// Internal remote client for Catalog basket validation.
/// </summary>
public sealed class CatalogValidationClient : ICatalogValidationClient
{
    /// <inheritdoc/>
    public async Task<ErrorOr<CatalogValidationResult>> ValidateAsync(
        IReadOnlyCollection<CatalogValidationItemRequest> items,
        CancellationToken cancellationToken)
    {
        ValidateProductsForBasketCommand command = new()
        {
            ServiceName = "basket",
        };

        foreach (CatalogValidationItemRequest item in items)
        {
            command.Items.Add(new ValidateProductsForBasketRpcItemRequest
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
            });
        }

        ValidateProductsForBasketRpcResult? response;
        try
        {
            response = await command
                .RemoteExecuteAsync(new CallOptions(cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }
        catch (RpcException exception)
        {
            return Error.Unexpected(
                "Basket.CatalogValidation.TransportFailure",
                $"Catalog validation RPC failed: {exception.StatusCode} ({exception.Status.Detail})");
        }

        if (response is null)
        {
            return Error.Unexpected(
                "Basket.CatalogValidation.EmptyResponse",
                "Catalog validation returned no response.");
        }

        if (!response.Succeeded)
        {
            return Error.Unexpected(
                "Basket.CatalogValidation.RemoteFailure",
                string.IsNullOrWhiteSpace(response.ErrorDetail)
                    ? "Catalog validation failed."
                    : response.ErrorDetail);
        }

        List<CatalogValidationItemResult> mapped = response.Items
            .Select(item => new CatalogValidationItemResult(
                item.ProductId,
                item.IsValid,
                item.UnitPrice,
                item.CurrencyCode,
                item.FailureCode))
            .ToList();

        return new CatalogValidationResult(mapped);
    }
}
