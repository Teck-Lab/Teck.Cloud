// <copyright file="CreateOrderFromBasketCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Order.Application.Common.Interfaces;
using Order.Application.Orders.Repositories;
using Order.Domain.Entities.OrderAggregate;
using SharedKernel.Core.CQRS;

namespace Order.Application.Orders.Features.CreateOrderFromBasket.V1;

/// <summary>
/// Command for creating order from a basket snapshot.
/// </summary>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="CustomerId">Customer identifier.</param>
/// <param name="BasketId">Basket identifier.</param>
public sealed record CreateOrderFromBasketCommand(Guid TenantId, Guid CustomerId, Guid BasketId)
    : ICommand<ErrorOr<CreateOrderFromBasketResponse>>;

/// <summary>
/// Handles order creation from basket snapshots.
/// </summary>
public sealed class CreateOrderFromBasketCommandHandler(
    IBasketSnapshotClient basketSnapshotClient,
    ICatalogValidationClient catalogValidationClient,
    IOrderRepository orderRepository)
    : ICommandHandler<CreateOrderFromBasketCommand, ErrorOr<CreateOrderFromBasketResponse>>
{
    private readonly IBasketSnapshotClient basketSnapshotClient = basketSnapshotClient;
    private readonly ICatalogValidationClient catalogValidationClient = catalogValidationClient;
    private readonly IOrderRepository orderRepository = orderRepository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<CreateOrderFromBasketResponse>> Handle(
        CreateOrderFromBasketCommand command,
        CancellationToken cancellationToken)
    {
        ErrorOr<BasketSnapshot> basketResult = await this.basketSnapshotClient
            .GetByIdAsync(command.BasketId, command.TenantId, command.CustomerId, cancellationToken)
            .ConfigureAwait(false);

        if (basketResult.IsError)
        {
            return basketResult.Errors;
        }

        BasketSnapshot basket = basketResult.Value;
        if (basket.Lines.Count == 0)
        {
            return Error.Validation("Order.Basket.Empty", "Basket has no lines to order");
        }

        List<CatalogValidationItemRequest> validationRequests = basket.Lines
            .Select(line => new CatalogValidationItemRequest(line.ProductId, line.Quantity))
            .ToList();

        ErrorOr<CatalogValidationResult> catalogValidationResult = await this.catalogValidationClient
            .ValidateAsync(validationRequests, cancellationToken)
            .ConfigureAwait(false);

        if (catalogValidationResult.IsError)
        {
            return catalogValidationResult.Errors;
        }

        CatalogValidationItemResult? failed = catalogValidationResult.Value.Items.FirstOrDefault(item => !item.IsValid);
        if (failed is not null)
        {
            string failureCode = string.IsNullOrWhiteSpace(failed.FailureCode)
                ? "catalog_revalidation_failed"
                : failed.FailureCode;
            return Error.Validation($"Order.CatalogValidation.{failureCode}", "Catalog revalidation failed at checkout");
        }

        Dictionary<Guid, CatalogValidationItemResult> validatedByProduct = catalogValidationResult.Value.Items
            .ToDictionary(item => item.ProductId);

        List<OrderLine> lines = basket.Lines
            .Select(line =>
            {
                CatalogValidationItemResult validated = validatedByProduct[line.ProductId];
                decimal unitPrice = validated.UnitPrice ?? line.UnitPrice;
                string currencyCode = string.IsNullOrWhiteSpace(validated.CurrencyCode)
                    ? line.CurrencyCode
                    : validated.CurrencyCode;
                return new OrderLine(line.ProductId, line.Quantity, unitPrice, currencyCode);
            })
            .ToList();

        OrderDraft order = OrderDraft.Create(command.TenantId, command.CustomerId, command.BasketId, lines);
        await this.orderRepository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

        return CreateOrderFromBasketResponse.FromDomain(order);
    }
}
