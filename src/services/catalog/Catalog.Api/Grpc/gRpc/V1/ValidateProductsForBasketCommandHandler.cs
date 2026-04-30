// <copyright file="ValidateProductsForBasketCommandHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Products.Features.ValidateProductsForBasket.V1;
using ErrorOr;
using Mediator;
using SharedKernel.Grpc.Contracts.Remote.V1.Catalog;

namespace Catalog.Api.Grpc.V1;

/// <summary>
/// Handles internal catalog basket validation RPC requests.
/// </summary>
/// <param name="sender">Mediator sender.</param>
internal sealed class ValidateProductsForBasketCommandHandler(ISender sender)
    : FastEndpoints.ICommandHandler<ValidateProductsForBasketCommand, ValidateProductsForBasketRpcResult>
{
    private readonly ISender sender = sender;

    /// <inheritdoc/>
    public async Task<ValidateProductsForBasketRpcResult> ExecuteAsync(ValidateProductsForBasketCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Items.Count == 0)
        {
            return new ValidateProductsForBasketRpcResult
            {
                Succeeded = false,
                ErrorDetail = "At least one basket line item is required.",
            };
        }

        if (command.Items.Any(item => item.ProductId == Guid.Empty || item.Quantity <= 0))
        {
            return new ValidateProductsForBasketRpcResult
            {
                Succeeded = false,
                ErrorDetail = "All line items must have a valid ProductId and Quantity greater than zero.",
            };
        }

        ValidateProductsForBasketQuery query = new(
            command.Items
                .Select(item => new ValidateProductsForBasketItemRequest
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                })
                .ToArray());

        ErrorOr<ValidateProductsForBasketResponse> queryResponse = await this.sender.Send(query, ct).ConfigureAwait(false);
        if (queryResponse.IsError)
        {
            string detail = string.Join(
                "; ",
                queryResponse.Errors.Select(error => $"{error.Code}:{error.Description}"));

            return new ValidateProductsForBasketRpcResult
            {
                Succeeded = false,
                ErrorDetail = string.IsNullOrWhiteSpace(detail)
                    ? "Catalog validation failed."
                    : detail,
            };
        }

        ValidateProductsForBasketResponse response = queryResponse.Value;
        ValidateProductsForBasketRpcResult result = new()
        {
            Succeeded = true,
            ValidatedAtUtc = response.ValidatedAtUtc,
        };

        foreach (ValidateProductsForBasketItemResponse item in response.Items)
        {
            result.Items.Add(new ValidateProductsForBasketRpcItemResult
            {
                ProductId = item.ProductId,
                IsValid = item.IsValid,
                UnitPrice = item.UnitPrice,
                CurrencyCode = item.CurrencyCode,
                FailureCode = item.FailureCode,
            });
        }

        return result;
    }
}
