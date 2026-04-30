// <copyright file="HybridBasketRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Basket.Application.Basket.Repositories;
using Basket.Domain.Entities.BasketAggregate;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace Basket.Infrastructure.Persistence;

/// <summary>
/// Repository that stores guest baskets in FusionCache/Redis and signed-in baskets in database plus cache.
/// </summary>
public sealed class HybridBasketRepository : IBasketRepository
{
    private static readonly FusionCacheEntryOptions CacheOptions = new FusionCacheEntryOptions()
        .SetDuration(TimeSpan.FromMinutes(15));

    private readonly IFusionCache cache;
    private readonly IDbContextFactory<BasketPersistenceDbContext> dbContextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="HybridBasketRepository"/> class.
    /// </summary>
    /// <param name="cache">Fusion cache instance.</param>
    /// <param name="dbContextFactory">Database context factory.</param>
    public HybridBasketRepository(
        IFusionCache cache,
        IDbContextFactory<BasketPersistenceDbContext> dbContextFactory)
    {
        this.cache = cache;
        this.dbContextFactory = dbContextFactory;
    }

    /// <inheritdoc/>
    public async Task<BasketDraft?> GetByTenantAndCustomerAsync(
        Guid tenantId,
        Guid customerId,
        bool isSignedIn,
        CancellationToken cancellationToken)
    {
        string cacheKey = BuildCacheKey(tenantId, customerId);

        var cachedResult = await this.cache
            .TryGetAsync<BasketCacheModel>(cacheKey, token: cancellationToken)
            .ConfigureAwait(false);

        if (cachedResult.HasValue && cachedResult.Value is not null)
        {
            return MapToDomain(cachedResult.Value);
        }

        if (!isSignedIn)
        {
            return null;
        }

        await using BasketPersistenceDbContext dbContext = await this.dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        SignedInBasketEntity? entity = await dbContext.Baskets
            .AsNoTracking()
            .Include(basket => basket.Lines)
            .SingleOrDefaultAsync(
                basket => basket.TenantId == tenantId && basket.CustomerId == customerId,
                cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        BasketCacheModel cachedBasket = MapToCacheModel(entity);
        await this.cache.SetAsync(cacheKey, cachedBasket, CacheOptions, token: cancellationToken).ConfigureAwait(false);

        return MapToDomain(cachedBasket);
    }

    /// <inheritdoc/>
    public async Task<BasketDraft?> GetByIdAsync(
        Guid basketId,
        Guid tenantId,
        Guid customerId,
        bool isSignedIn,
        CancellationToken cancellationToken)
    {
        string cacheKey = BuildCacheKey(tenantId, customerId);

        var cachedResult = await this.cache
            .TryGetAsync<BasketCacheModel>(cacheKey, token: cancellationToken)
            .ConfigureAwait(false);

        if (cachedResult.HasValue && cachedResult.Value is not null && cachedResult.Value.BasketId == basketId)
        {
            return MapToDomain(cachedResult.Value);
        }

        if (!isSignedIn)
        {
            return null;
        }

        await using BasketPersistenceDbContext dbContext = await this.dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        SignedInBasketEntity? entity = await dbContext.Baskets
            .AsNoTracking()
            .Include(basket => basket.Lines)
            .SingleOrDefaultAsync(
                basket => basket.Id == basketId &&
                          basket.TenantId == tenantId &&
                          basket.CustomerId == customerId,
                cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        BasketCacheModel cachedBasket = MapToCacheModel(entity);
        await this.cache.SetAsync(cacheKey, cachedBasket, CacheOptions, token: cancellationToken).ConfigureAwait(false);

        return MapToDomain(cachedBasket);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(BasketDraft basket, bool isSignedIn, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(basket);

        string cacheKey = BuildCacheKey(basket.TenantId, basket.CustomerId);
        BasketCacheModel cacheModel = MapToCacheModel(basket);

        await this.cache.SetAsync(cacheKey, cacheModel, CacheOptions, token: cancellationToken).ConfigureAwait(false);

        if (!isSignedIn)
        {
            return;
        }

        await using BasketPersistenceDbContext dbContext = await this.dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        SignedInBasketEntity? existingBasket = await dbContext.Baskets
            .Include(dbBasket => dbBasket.Lines)
            .SingleOrDefaultAsync(
                dbBasket => dbBasket.TenantId == basket.TenantId && dbBasket.CustomerId == basket.CustomerId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existingBasket is null)
        {
            SignedInBasketEntity newBasket = new()
            {
                Id = basket.Id,
                TenantId = basket.TenantId,
                CustomerId = basket.CustomerId,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            foreach (BasketLine line in basket.Lines)
            {
                newBasket.Lines.Add(new SignedInBasketLineEntity
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    CurrencyCode = line.CurrencyCode,
                });
            }

            dbContext.Baskets.Add(newBasket);
        }
        else
        {
            dbContext.BasketLines.RemoveRange(existingBasket.Lines);
            existingBasket.UpdatedAt = DateTimeOffset.UtcNow;
            existingBasket.Lines.Clear();

            foreach (BasketLine line in basket.Lines)
            {
                existingBasket.Lines.Add(new SignedInBasketLineEntity
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

    private static BasketDraft MapToDomain(BasketCacheModel cacheModel)
    {
        List<BasketLine> lines = cacheModel.Lines
            .Select(line => new BasketLine(line.ProductId, line.Quantity, line.UnitPrice, line.CurrencyCode))
            .ToList();

        return BasketDraft.Rehydrate(cacheModel.BasketId, cacheModel.TenantId, cacheModel.CustomerId, lines);
    }

    private static BasketCacheModel MapToCacheModel(BasketDraft basket)
    {
        return new BasketCacheModel
        {
            BasketId = basket.Id,
            TenantId = basket.TenantId,
            CustomerId = basket.CustomerId,
            Lines = basket.Lines
                .Select(line => new BasketCacheLineModel
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    CurrencyCode = line.CurrencyCode,
                })
                .ToList(),
        };
    }

    private static BasketCacheModel MapToCacheModel(SignedInBasketEntity entity)
    {
        return new BasketCacheModel
        {
            BasketId = entity.Id,
            TenantId = entity.TenantId,
            CustomerId = entity.CustomerId,
            Lines = entity.Lines
                .Select(line => new BasketCacheLineModel
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    CurrencyCode = line.CurrencyCode,
                })
                .ToList(),
        };
    }

    private static string BuildCacheKey(Guid tenantId, Guid customerId)
    {
        return $"basket:draft:{tenantId:D}:{customerId:D}";
    }

    private sealed class BasketCacheModel
    {
        public Guid BasketId { get; init; }

        public Guid TenantId { get; init; }

        public Guid CustomerId { get; init; }

        public List<BasketCacheLineModel> Lines { get; init; } = [];
    }

    private sealed class BasketCacheLineModel
    {
        public Guid ProductId { get; init; }

        public int Quantity { get; init; }

        public decimal UnitPrice { get; init; }

        public string CurrencyCode { get; init; } = string.Empty;
    }
}

/// <summary>
/// EF Core persistence context for signed-in basket storage.
/// </summary>
public sealed class BasketPersistenceDbContext(DbContextOptions<BasketPersistenceDbContext> options)
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

/// <summary>
/// Signed-in basket aggregate persistence model.
/// </summary>
public sealed class SignedInBasketEntity
{
    /// <summary>
    /// Gets or sets basket identifier.
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
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets line items.
    /// </summary>
    public ICollection<SignedInBasketLineEntity> Lines { get; } = [];
}

/// <summary>
/// Signed-in basket line persistence model.
/// </summary>
public sealed class SignedInBasketLineEntity
{
    /// <summary>
    /// Gets or sets line identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets parent basket identifier.
    /// </summary>
    public Guid BasketId { get; set; }

    /// <summary>
    /// Gets or sets product identifier.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Gets or sets quantity.
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
    /// Gets or sets parent basket.
    /// </summary>
    public SignedInBasketEntity Basket { get; set; } = null!;
}
