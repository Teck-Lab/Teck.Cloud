using ErrorOr;
using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.Catalog;
using Teck.Cloud.Commerce.Orders.Application.Orders.ReadModels;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Application.Catalog.Features.GetCatalog.V1;

/// <summary>Catalog query handler.</summary>
public static class GetCatalogHandler
{
    /// <summary>Projects the advertised catalog.</summary>
    /// <param name="query">The query.</param>
    /// <param name="orders">An unused read dependency retained by the query-handler contract.</param>
    /// <param name="catalog">The server catalog.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The advertised offers.</returns>
    public static Task<ErrorOr<IReadOnlyList<OfferDto>>> HandleAsync(
        GetCatalogQuery query,
        IGenericReadRepository<OrderReadModel, Guid> orders,
        CatalogService catalog,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(catalog);
        cancellationToken.ThrowIfCancellationRequested();
        _ = orders;
        IReadOnlyList<OfferDto> offers = [
            .. catalog.GetOffers().Select(static offer => new OfferDto(
                offer.OfferId,
                offer.Tier,
                offer.DisplayName,
                new MoneyDto(offer.AmountMinor, offer.Currency),
                [
                    .. offer.AddOns.Select(addOn => new AddOnDto(
                        addOn.Code,
                        addOn.DisplayName,
                        new MoneyDto(addOn.AmountMinor, offer.Currency))),
                ])),
        ];
        return ErrorOrFactory.FromAsync(offers);
    }
}
