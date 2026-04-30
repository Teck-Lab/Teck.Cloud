// <copyright file="AddItemToBasketCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Basket.Application.Basket.Repositories;
using Basket.Application.Common.Interfaces;
using Basket.Domain.Entities.BasketAggregate;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Basket.Application.Basket.Features.AddItemToBasket.V1;

/// <summary>
/// Command to add a product line to basket.
/// </summary>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="CustomerId">Customer identifier.</param>
/// <param name="IsSignedIn">Whether the basket owner is authenticated.</param>
/// <param name="ProductId">Product identifier.</param>
/// <param name="Quantity">Quantity to add.</param>
public sealed record AddItemToBasketCommand(
    Guid TenantId,
    Guid CustomerId,
    bool IsSignedIn,
    Guid ProductId,
    int Quantity) : ICommand<ErrorOr<AddItemToBasketResponse>>;

/// <summary>
/// Handles add item to basket command.
/// </summary>
public sealed class AddItemToBasketCommandHandler(
    IBasketRepository basketRepository,
    ICatalogValidationClient catalogValidationClient)
    : ICommandHandler<AddItemToBasketCommand, ErrorOr<AddItemToBasketResponse>>
{
    private readonly IBasketRepository basketRepository = basketRepository;
    private readonly ICatalogValidationClient catalogValidationClient = catalogValidationClient;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<AddItemToBasketResponse>> Handle(
        AddItemToBasketCommand command,
        CancellationToken cancellationToken)
    {
        ErrorOr<CatalogValidationResult> validationResult = await this.catalogValidationClient
            .ValidateAsync(
                [new CatalogValidationItemRequest(command.ProductId, command.Quantity)],
                cancellationToken)
            .ConfigureAwait(false);

        if (validationResult.IsError)
        {
            return validationResult.Errors;
        }

        CatalogValidationItemResult? validatedLine = validationResult.Value.Items
            .FirstOrDefault(item => item.ProductId == command.ProductId);

        if (validatedLine is null)
        {
            return Error.Unexpected(
                "Basket.CatalogValidation.MissingLine",
                "Catalog validation response did not contain the requested product");
        }

        if (!validatedLine.IsValid || validatedLine.UnitPrice is null || string.IsNullOrWhiteSpace(validatedLine.CurrencyCode))
        {
            string failureCode = string.IsNullOrWhiteSpace(validatedLine.FailureCode)
                ? "product_invalid_for_basket"
                : validatedLine.FailureCode;
            return Error.Validation($"Basket.Validation.{failureCode}", "Product could not be added to basket");
        }

        BasketDraft basket = await this.basketRepository
            .GetByTenantAndCustomerAsync(command.TenantId, command.CustomerId, command.IsSignedIn, cancellationToken)
            .ConfigureAwait(false)
            ?? BasketDraft.Create(command.TenantId, command.CustomerId);

        basket.AddOrUpdateLine(command.ProductId, command.Quantity, validatedLine.UnitPrice.Value, validatedLine.CurrencyCode);

        await this.basketRepository.SaveAsync(basket, command.IsSignedIn, cancellationToken).ConfigureAwait(false);

        return AddItemToBasketResponse.FromDomain(basket);
    }
}
