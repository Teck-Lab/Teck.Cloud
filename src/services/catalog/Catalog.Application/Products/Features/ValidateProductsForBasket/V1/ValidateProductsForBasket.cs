// <copyright file="ValidateProductsForBasket.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Repositories;
using Catalog.Application.Promotions.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Catalog.Application.Products.Features.ValidateProductsForBasket.V1;

/// <summary>
/// Query for validating basket line items against catalog data.
/// </summary>
/// <param name="Items">Items to validate.</param>
public sealed record ValidateProductsForBasketQuery(IReadOnlyCollection<ValidateProductsForBasketItemRequest> Items)
    : IQuery<ErrorOr<ValidateProductsForBasketResponse>>;

/// <summary>
/// Handler for basket line item validation.
/// </summary>
internal sealed class ValidateProductsForBasketQueryHandler(
    IProductReadRepository productReadRepository,
    IProductPriceReadRepository productPriceReadRepository,
    IPromotionReadRepository promotionReadRepository)
    : IQueryHandler<ValidateProductsForBasketQuery, ErrorOr<ValidateProductsForBasketResponse>>
{
    private readonly IProductReadRepository productReadRepository = productReadRepository;
    private readonly IProductPriceReadRepository productPriceReadRepository = productPriceReadRepository;
    private readonly IPromotionReadRepository promotionReadRepository = promotionReadRepository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<ValidateProductsForBasketResponse>> Handle(
        ValidateProductsForBasketQuery request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid[] productIds = request.Items
            .Select(item => item.ProductId)
            .Distinct()
            .ToArray();

        IReadOnlyList<ProductReadModel> products = await this.productReadRepository
            .GetByIdsAsync(productIds, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ProductPriceReadModel> productPrices = await this.productPriceReadRepository
            .GetByProductIdsAsync(productIds, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Catalog.Application.Promotions.ReadModels.PromotionReadModel> activePromotions = await this.promotionReadRepository
            .GetActivePromotionsAsync(cancellationToken)
            .ConfigureAwait(false);
        bool hasAnyActiveRebate = activePromotions.Count != 0;

        Dictionary<Guid, ProductReadModel> productLookup = products.ToDictionary(product => product.Id);
        Dictionary<Guid, IReadOnlyList<ProductPriceReadModel>> priceLookup = productPrices
            .GroupBy(price => price.ProductId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProductPriceReadModel>)group.ToList());

        List<ValidateProductsForBasketItemResponse> items = request.Items
            .Select(item => BuildItemResponse(item, productLookup, priceLookup, hasAnyActiveRebate))
            .ToList();

        return new ValidateProductsForBasketResponse
        {
            ValidatedAtUtc = now,
            Items = items,
        };
    }

    private static ValidateProductsForBasketItemResponse BuildItemResponse(
        ValidateProductsForBasketItemRequest item,
        IReadOnlyDictionary<Guid, ProductReadModel> productLookup,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProductPriceReadModel>> priceLookup,
        bool hasAnyActiveRebate)
    {
        if (!productLookup.TryGetValue(item.ProductId, out ProductReadModel? product))
        {
            return new ValidateProductsForBasketItemResponse
            {
                ProductId = item.ProductId,
                RequestedQuantity = item.Quantity,
                Exists = false,
                IsValid = false,
                QuantityWithinLimits = true,
                FailureCode = "product_not_found",
            };
        }

        priceLookup.TryGetValue(item.ProductId, out IReadOnlyList<ProductPriceReadModel>? linePrices);

        ProductPriceReadModel? selectedPrice = linePrices?
            .OrderBy(price => price.SalePrice)
            .FirstOrDefault();

        bool isValid = product.IsActive && selectedPrice is not null;

        return new ValidateProductsForBasketItemResponse
        {
            ProductId = item.ProductId,
            RequestedQuantity = item.Quantity,
            Exists = true,
            IsActive = product.IsActive,
            UnitPrice = selectedPrice?.SalePrice,
            CurrencyCode = selectedPrice?.CurrencyCode,
            HasActiveRebate = hasAnyActiveRebate,
            QuantityWithinLimits = true,
            IsValid = isValid,
            FailureCode = ResolveFailureCode(product, selectedPrice),
        };
    }

    private static string? ResolveFailureCode(ProductReadModel product, ProductPriceReadModel? price)
    {
        if (!product.IsActive)
        {
            return "product_inactive";
        }

        if (price is null)
        {
            return "price_unavailable";
        }

        return null;
    }
}
