// <copyright file="CatalogValidationClient.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using FastEndpoints;
using Grpc.Core;
using Order.Application.Common.Interfaces;
using SharedKernel.Grpc.Contracts.Remote.V1.Catalog;

namespace Order.Infrastructure.Catalog;

/// <summary>
/// Internal remote client for Catalog product validation.
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
            ServiceName = "order",
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
                "Order.CatalogValidation.TransportFailure",
                $"Catalog validation RPC failed: {exception.StatusCode} ({exception.Status.Detail})");
        }

        if (response is null)
        {
            return Error.Unexpected(
                "Order.CatalogValidation.EmptyResponse",
                "Catalog validation returned no response.");
        }

        if (!response.Succeeded)
        {
            return Error.Unexpected(
                "Order.CatalogValidation.RemoteFailure",
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
