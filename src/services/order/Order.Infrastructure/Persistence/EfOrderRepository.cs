// <copyright file="EfOrderRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Order.Application.Orders.Repositories;
using Order.Domain.Entities.OrderAggregate;

namespace Order.Infrastructure.Persistence;

/// <summary>
/// EF Core-backed repository for order drafts.
/// </summary>
public sealed class EfOrderRepository(IDbContextFactory<OrderPersistenceDbContext> dbContextFactory)
    : IOrderRepository
{
    private readonly IDbContextFactory<OrderPersistenceDbContext> dbContextFactory = dbContextFactory;

    /// <inheritdoc/>
    public async Task SaveAsync(OrderDraft order, CancellationToken cancellationToken)
    {
        await using OrderPersistenceDbContext dbContext = await this.dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        OrderDraftEntity? existingOrder = await dbContext.Orders
            .Include(entity => entity.Lines)
            .SingleOrDefaultAsync(entity => entity.Id == order.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existingOrder is null)
        {
            dbContext.Orders.Add(Map(order));
        }
        else
        {
            existingOrder.TenantId = order.TenantId;
            existingOrder.CustomerId = order.CustomerId;
            existingOrder.BasketId = order.BasketId;
            existingOrder.Status = order.Status;
            existingOrder.CreatedAtUtc = order.CreatedAtUtc;

            dbContext.OrderLines.RemoveRange(existingOrder.Lines);
            existingOrder.Lines.Clear();

            foreach (OrderLine line in order.Lines)
            {
                existingOrder.Lines.Add(new OrderLineEntity
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    CurrencyCode = line.CurrencyCode,
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static OrderDraftEntity Map(OrderDraft order)
    {
        var entity = new OrderDraftEntity
        {
            Id = order.Id,
            TenantId = order.TenantId,
            CustomerId = order.CustomerId,
            BasketId = order.BasketId,
            Status = order.Status,
            CreatedAtUtc = order.CreatedAtUtc,
        };

        foreach (OrderLine line in order.Lines)
        {
            entity.Lines.Add(new OrderLineEntity
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                CurrencyCode = line.CurrencyCode,
            });
        }

        return entity;
    }
}
