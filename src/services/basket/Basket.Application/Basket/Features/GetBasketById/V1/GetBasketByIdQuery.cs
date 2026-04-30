// <copyright file="GetBasketByIdQuery.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Basket.Application.Basket.Features.AddItemToBasket.V1;
using Basket.Application.Basket.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Basket.Application.Basket.Features.GetBasketById.V1;

/// <summary>
/// Query to retrieve a basket snapshot by identifier.
/// </summary>
/// <param name="BasketId">Basket identifier.</param>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="CustomerId">Customer identifier.</param>
/// <param name="IsSignedIn">Whether the basket owner is authenticated.</param>
public sealed record GetBasketByIdQuery(Guid BasketId, Guid TenantId, Guid CustomerId, bool IsSignedIn)
    : IQuery<ErrorOr<AddItemToBasketResponse>>;

/// <summary>
/// Handles basket lookup requests.
/// </summary>
public sealed class GetBasketByIdQueryHandler(IBasketRepository basketRepository)
    : IQueryHandler<GetBasketByIdQuery, ErrorOr<AddItemToBasketResponse>>
{
    private readonly IBasketRepository basketRepository = basketRepository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<AddItemToBasketResponse>> Handle(
        GetBasketByIdQuery query,
        CancellationToken cancellationToken)
    {
        var basket = await this.basketRepository
            .GetByIdAsync(query.BasketId, query.TenantId, query.CustomerId, query.IsSignedIn, cancellationToken)
            .ConfigureAwait(false);

        if (basket is null)
        {
            return Error.NotFound("Basket.NotFound", $"Basket '{query.BasketId}' was not found");
        }

        return AddItemToBasketResponse.FromDomain(basket);
    }
}
