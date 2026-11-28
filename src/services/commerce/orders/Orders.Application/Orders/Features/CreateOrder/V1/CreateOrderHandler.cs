using ErrorOr;
using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Orders.Application.Baskets.Specifications;
using Teck.Cloud.Commerce.Orders.Application.Orders.Billing;
using Teck.Cloud.Commerce.Orders.Application.Orders.Catalog;
using Teck.Cloud.Commerce.Orders.Application.Orders.Checkout;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Baskets;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Idempotency;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Reconciliation;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.CreateOrder.V1;

/// <summary>Checkout creation and same-intent replay.</summary>
public static class CreateOrderHandler
{
    /// <summary>Handles the command inside the caller's Wolverine transaction and outbox.</summary>
    /// <param name="command">The command.</param>
    /// <param name="orders">The tracked order repository.</param>
    /// <param name="baskets">The tracked basket repository.</param>
    /// <param name="receipts">The tracked idempotency receipt repository.</param>
    /// <param name="jobs">The tracked reconciliation job repository.</param>
    /// <param name="catalog">The server catalog.</param>
    /// <param name="intentLock">The cross-process intent lock.</param>
    /// <param name="unitOfWork">The transaction save boundary.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The checkout result, or a stable error.</returns>
    public static Task<ErrorOr<CheckoutResultDto>> HandleAsync(
        CreateOrderCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<Basket, Guid> baskets,
        IGenericWriteRepository<IdempotencyReceipt, Guid> receipts,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        CatalogService catalog,
        ICheckoutIntentLock intentLock,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleCoreAsync(command, orders, baskets, receipts, jobs, catalog, intentLock, unitOfWork, clock, cancellationToken);
    }

    private static async Task<ErrorOr<CheckoutResultDto>> HandleCoreAsync(
        CreateOrderCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<Basket, Guid> baskets,
        IGenericWriteRepository<IdempotencyReceipt, Guid> receipts,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        CatalogService catalog,
        ICheckoutIntentLock intentLock,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(command.IdempotencyKey, "D", out var key))
        {
            return OrderErrors.InvalidIdempotencyKey;
        }

        var now = clock.GetUtcNow();
        var intent = await intentLock.AcquireAsync(command.TenantId, command.SubjectId, cancellationToken).ConfigureAwait(false);
        await using var configuredIntent = intent.ConfigureAwait(false);
        var canonicalKey = key.ToString("D");
        var receipt = await receipts.FindOneAsync(
            candidate => candidate.SubjectId == command.SubjectId && candidate.IdempotencyKey == canonicalKey,
            cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            return await ReplayAsync(command, orders, jobs, unitOfWork, receipt, now, cancellationToken).ConfigureAwait(false);
        }

        var basket = await baskets.LoadForUpdateAsync(
            new BasketBySubjectSpecification(command.TenantId, command.SubjectId),
            cancellationToken).ConfigureAwait(false);
        return basket is null
            ? BasketErrors.Empty
            : await CreateAsync(command, orders, receipts, jobs, catalog, basket, unitOfWork, canonicalKey, now, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ErrorOr<CheckoutResultDto>> ReplayAsync(
        CreateOrderCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        IUnitOfWork unitOfWork,
        IdempotencyReceipt receipt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (receipt.BasketRevision != command.BasketRevision)
        {
            return OrderErrors.IdempotencyConflict;
        }

        var bound = await orders.FindByIdAsync(receipt.OrderId, cancellationToken).ConfigureAwait(false);
        if (bound is null)
        {
            return OrderErrors.NotFound;
        }

        await EnsureCheckoutWorkAsync(bound, jobs, now, cancellationToken).ConfigureAwait(false);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new CheckoutResultDto(
            OrderProjection.Customer(OrderProjection.From(bound), now),
            bound.HasUsableCheckoutUrlAt(now) ? bound.CheckoutUrl : null);
    }

    private static async Task<ErrorOr<CheckoutResultDto>> CreateAsync(
        CreateOrderCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<IdempotencyReceipt, Guid> receipts,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        CatalogService catalog,
        Basket basket,
        IUnitOfWork unitOfWork,
        string canonicalKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var selection = basket.RequireCheckoutSelection(command.BasketRevision);
        if (selection.IsError)
        {
            return selection.FirstError;
        }

        var resolved = catalog.Resolve(selection.Value);
        if (resolved.IsError)
        {
            return resolved.FirstError;
        }

        var orderId = CheckoutIds.OrderId(command.TenantId, command.SubjectId, canonicalKey);
        var order = Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders.Order.Create(
            command.TenantId,
            command.SubjectId,
            orderId,
            Guid.NewGuid(),
            selection.Value,
            resolved.Value.Tier,
            resolved.Value.Total,
            PriceSnapshot.EncodeCatalog(resolved.Value.Price),
            now);
        var createdReceipt = IdempotencyReceipt.Create(
            command.TenantId,
            command.SubjectId,
            canonicalKey,
            command.BasketRevision,
            orderId);
        var job = ReconciliationJob.Create(
            command.TenantId,
            orderId,
            ReconciliationKind.CheckoutSession,
            now,
            now);
        var cleared = basket.ClearAfterCheckout(now);
        if (cleared.IsError)
        {
            return cleared.FirstError;
        }

        await orders.AddAsync(order, cancellationToken).ConfigureAwait(false);
        await receipts.AddAsync(createdReceipt, cancellationToken).ConfigureAwait(false);
        await jobs.AddAsync(job, cancellationToken).ConfigureAwait(false);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new CheckoutResultDto(OrderProjection.Customer(OrderProjection.From(order), now), CheckoutUrl: null);
    }

    private static async Task EnsureCheckoutWorkAsync(
        Order order,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!order.CanCheckoutAt(now) || order.HasUsableCheckoutUrlAt(now))
        {
            return;
        }

        var existing = await jobs.FindOneAsync(
            candidate => candidate.OrderId == order.Id && candidate.Kind == ReconciliationKind.CheckoutSession,
            cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            await jobs.AddAsync(
                ReconciliationJob.Create(order.TenantId, order.Id, ReconciliationKind.CheckoutSession, now, now),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
