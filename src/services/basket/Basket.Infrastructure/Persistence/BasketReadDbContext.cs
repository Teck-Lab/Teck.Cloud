// <copyright file="BasketReadDbContext.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace Basket.Infrastructure.Persistence;

/// <summary>
/// EF Core read context for signed-in basket storage.
/// </summary>
/// <param name="options">DbContext options.</param>
public sealed class BasketReadDbContext(DbContextOptions<BasketReadDbContext> options)
    : DbContext(options)
{
    /// <summary>
    /// Gets the signed-in baskets table.
    /// </summary>
    public DbSet<SignedInBasketEntity> Baskets => this.Set<SignedInBasketEntity>();

    /// <summary>
    /// Gets the signed-in basket lines table.
    /// </summary>
    public DbSet<SignedInBasketLineEntity> BasketLines => this.Set<SignedInBasketLineEntity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SignedInBasketEntity>(entity =>
        {
            entity.ToTable("BasketDrafts");
            entity.HasKey(basket => basket.Id);
            entity.HasIndex(basket => new { basket.TenantId, basket.CustomerId }).IsUnique();
            entity.Property(basket => basket.UpdatedAt).IsRequired();

            entity
                .HasMany(basket => basket.Lines)
                .WithOne(line => line.Basket)
                .HasForeignKey(line => line.BasketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SignedInBasketLineEntity>(entity =>
        {
            entity.ToTable("BasketDraftLines");
            entity.HasKey(line => line.Id);
            entity.Property(line => line.CurrencyCode).HasMaxLength(8).IsRequired();
            entity.Property(line => line.UnitPrice).HasPrecision(18, 4);
        });
    }
}
