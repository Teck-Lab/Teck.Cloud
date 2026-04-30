// <copyright file="GetCatalogReadinessSummary.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Brands.ReadModels;
using Catalog.Application.Brands.Repositories;
using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Repositories;
using Catalog.Application.Promotions.ReadModels;
using Catalog.Application.Promotions.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Catalog.Application.Service.Features.GetCatalogReadinessSummary.V1;

/// <summary>
/// Query for catalog readiness summary counters.
/// </summary>
public sealed record GetCatalogReadinessSummaryQuery : IQuery<ErrorOr<GetCatalogReadinessSummaryResponse>>;

/// <summary>
/// Handler for catalog readiness summary.
/// </summary>
internal sealed class GetCatalogReadinessSummaryQueryHandler(
    IBrandReadRepository brandReadRepository,
    IProductReadRepository productReadRepository,
    IPromotionReadRepository promotionReadRepository)
    : IQueryHandler<GetCatalogReadinessSummaryQuery, ErrorOr<GetCatalogReadinessSummaryResponse>>
{
    private readonly IBrandReadRepository brandReadRepository = brandReadRepository;
    private readonly IProductReadRepository productReadRepository = productReadRepository;
    private readonly IPromotionReadRepository promotionReadRepository = promotionReadRepository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<GetCatalogReadinessSummaryResponse>> Handle(
        GetCatalogReadinessSummaryQuery request,
        CancellationToken cancellationToken)
    {
        Task<IReadOnlyList<BrandReadModel>> brandsTask = this.brandReadRepository
            .GetAllAsync(cancellationToken: cancellationToken);
        Task<IReadOnlyList<ProductReadModel>> productsTask = this.productReadRepository
            .GetAllAsync(cancellationToken);
        Task<IReadOnlyList<PromotionReadModel>> activePromotionsTask = this.promotionReadRepository
            .GetActivePromotionsAsync(cancellationToken);

        await Task.WhenAll(brandsTask, productsTask, activePromotionsTask).ConfigureAwait(false);

        IReadOnlyList<BrandReadModel> brands = await brandsTask.ConfigureAwait(false);
        IReadOnlyList<ProductReadModel> products = await productsTask.ConfigureAwait(false);
        IReadOnlyList<PromotionReadModel> activePromotions = await activePromotionsTask.ConfigureAwait(false);

        return new GetCatalogReadinessSummaryResponse
        {
            BrandCount = brands.Count,
            ProductCount = products.Count,
            ActivePromotionCount = activePromotions.Count,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
