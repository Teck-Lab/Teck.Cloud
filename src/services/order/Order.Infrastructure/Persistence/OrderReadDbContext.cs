// <copyright file="OrderReadDbContext.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace Order.Infrastructure.Persistence;

/// <summary>
/// EF Core read context for order drafts.
/// </summary>
/// <param name="options">DbContext options.</param>
public sealed class OrderReadDbContext(DbContextOptions<OrderReadDbContext> options)
    : DbContext(options)
{
    /// <summary>
    /// Gets persisted order drafts.
    /// </summary>
    public DbSet<OrderDraftEntity> Orders => this.Set<OrderDraftEntity>();

    /// <summary>
    /// Gets persisted order lines.
    /// </summary>
    public DbSet<OrderLineEntity> OrderLines => this.Set<OrderLineEntity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderDraftEntity>(entity =>
        {
            entity.ToTable("OrderDrafts");
            entity.HasKey(order => order.Id);
            entity.Property(order => order.Status).HasMaxLength(32).IsRequired();
            entity.Property(order => order.CreatedAtUtc).IsRequired();

            entity
                .HasMany(order => order.Lines)
                .WithOne(line => line.Order)
                .HasForeignKey(line => line.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderLineEntity>(entity =>
        {
            entity.ToTable("OrderDraftLines");
            entity.HasKey(line => line.Id);
            entity.Property(line => line.CurrencyCode).HasMaxLength(8).IsRequired();
            entity.Property(line => line.UnitPrice).HasPrecision(18, 4);
        });
    }
}
