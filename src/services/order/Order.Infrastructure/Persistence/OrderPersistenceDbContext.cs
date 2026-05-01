// <copyright file="OrderPersistenceDbContext.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace Order.Infrastructure.Persistence;

/// <summary>
/// EF Core persistence context for order drafts.
/// </summary>
public sealed class OrderPersistenceDbContext(DbContextOptions<OrderPersistenceDbContext> options)
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

/// <summary>
/// Order draft persistence model.
/// </summary>
public sealed class OrderDraftEntity
{
    /// <summary>
    /// Gets or sets order identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets tenant identifier.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Gets or sets customer identifier.
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Gets or sets source basket identifier.
    /// </summary>
    public Guid BasketId { get; set; }

    /// <summary>
    /// Gets or sets order status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets order lines.
    /// </summary>
    public ICollection<OrderLineEntity> Lines { get; } = [];
}

/// <summary>
/// Order line persistence model.
/// </summary>
public sealed class OrderLineEntity
{
    /// <summary>
    /// Gets or sets line identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets parent order identifier.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Gets or sets product identifier.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Gets or sets ordered quantity.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets unit price.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets currency code.
    /// </summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets parent order.
    /// </summary>
    public OrderDraftEntity Order { get; set; } = null!;
}
