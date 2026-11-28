using ErrorOr;
using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Orders.Application.Baskets.Specifications;
using Teck.Cloud.Commerce.Orders.Application.Orders.Catalog;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Baskets;

namespace Teck.Cloud.Commerce.Orders.Application.Baskets.Features.PutBasket.V1;

/// <summary>Basket mutation handler.</summary>
public static class PutBasketHandler
{
    /// <summary>Handles the bucket mutation inside the caller's transaction.</summary>
    /// <param name="command">The command.</param>
    /// <param name="baskets">The tracked basket repository.</param>
    /// <param name="catalog">The server catalog.</param>
    /// <param name="unitOfWork">The save boundary.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The updated basket, or a stable error.</returns>
    public static Task<ErrorOr<BasketDto>> HandleAsync(
        PutBasketCommand command,
        IGenericWriteRepository<Basket, Guid> baskets,
        CatalogService catalog,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleCoreAsync(command, baskets, catalog, unitOfWork, clock, cancellationToken);
    }

    private static async Task<ErrorOr<BasketDto>> HandleCoreAsync(
        PutBasketCommand command,
        IGenericWriteRepository<Basket, Guid> baskets,
        CatalogService catalog,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var basket = await baskets.LoadForUpdateAsync(
            new BasketBySubjectSpecification(command.TenantId, command.SubjectId),
            cancellationToken).ConfigureAwait(false);
        if (command.OfferId is null)
        {
            if (basket is null)
            {
                return new BasketDto(Selection: null, Revision: command.ExpectedRevision, Total: null);
            }

            var cleared = basket.Clear(command.ExpectedRevision, now);
            if (cleared.IsError)
            {
                return cleared.FirstError;
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return OrderProjection.Basket(basket);
        }

        if (basket is null)
        {
            if (command.ExpectedRevision != 0)
            {
                return BasketErrors.StaleRevision;
            }

            basket = Basket.CreateEmpty(command.TenantId, command.SubjectId);
            await baskets.AddAsync(basket, cancellationToken).ConfigureAwait(false);
        }

        var canonical = BasketSelection.Create(command.OfferId, command.AddOnCodes);
        if (canonical.IsError)
        {
            return canonical.FirstError;
        }

        var resolved = catalog.Resolve(canonical.Value);
        if (resolved.IsError)
        {
            return resolved.FirstError;
        }

        var replaced = basket.Replace(canonical.Value, resolved.Value.Total, command.ExpectedRevision, now);
        if (replaced.IsError)
        {
            return replaced.FirstError;
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return OrderProjection.Basket(basket);
    }
}
