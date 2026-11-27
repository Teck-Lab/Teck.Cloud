using ErrorOr;
using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.ReadModels;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Application.Baskets.Features.GetBasket.V1;

/// <summary>Basket query handler.</summary>
public static class GetBasketHandler
{
    /// <summary>Reads the basket without tracking.</summary>
    /// <param name="query">The query.</param>
    /// <param name="baskets">The read-only basket repository.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The basket, or an empty revision-zero projection.</returns>
    public static Task<ErrorOr<BasketDto>> HandleAsync(
        GetBasketQuery query,
        IGenericReadRepository<BasketReadModel, Guid> baskets,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleCoreAsync(query, baskets, cancellationToken);
    }

    private static async Task<ErrorOr<BasketDto>> HandleCoreAsync(
        GetBasketQuery query,
        IGenericReadRepository<BasketReadModel, Guid> baskets,
        CancellationToken cancellationToken)
    {
        var basket = await baskets.FindOneAsync(
            candidate => candidate.SubjectId == query.SubjectId,
            cancellationToken).ConfigureAwait(false);
        return basket is null
            ? new BasketDto(Selection: null, Revision: 0, Total: null)
            : OrderProjection.Basket(basket);
    }
}
