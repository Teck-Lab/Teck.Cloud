using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SharedKernel.Core.Database;
using SharedKernel.Core.Domain;
using SharedKernel.Core.Events;
using SharedKernel.Core.MultiTenancy;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Persistence;

namespace SharedKernel.Persistence.Database;

/// <summary>Opt-in persistence registrations; providers, contexts and migrations remain service-owned.</summary>
public static class PersistenceExtensions
{
    /// <summary>Registers scoped operation identity and optional audit behavior.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The configured registrations for chaining.</returns>
    public static IServiceCollection AddPersistenceFoundation(this IServiceCollection services)
    {
        services.TryAddScoped<OperationContext>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<AuditAndSoftDeleteInterceptor>();
        return services;
    }

    /// <summary>Registers a read-only repository for a projection, root or child entity.</summary>
    /// <typeparam name="TEntity">The readable model.</typeparam>
    /// <typeparam name="TId">Its identifier.</typeparam>
    /// <typeparam name="TRead">The service's read context.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The configured registrations for chaining.</returns>
    public static IServiceCollection AddReadRepository<TEntity, TId, TRead>(this IServiceCollection services)
        where TEntity : class, IReadModel<TId>
        where TRead : ScopedDbContext
    {
        _ = services.AddScoped<IGenericReadRepository<TEntity, TId>, GenericReadRepository<TEntity, TId, TRead>>();
        return services;
    }

    /// <summary>Registers a tracked write repository for an aggregate root independently of the read model.</summary>
    /// <typeparam name="TEntity">The write aggregate.</typeparam>
    /// <typeparam name="TId">Its identifier.</typeparam>
    /// <typeparam name="TWrite">The service's write context.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The configured registrations for chaining.</returns>
    public static IServiceCollection AddWriteRepository<TEntity, TId, TWrite>(this IServiceCollection services)
        where TEntity : class, IReadModel<TId>, IAggregateRoot
        where TWrite : WriteDbContext
    {
        _ = services.AddScoped<IGenericWriteRepository<TEntity, TId>, GenericWriteRepository<TEntity, TId, TWrite>>();
        return services;
    }

    /// <summary>Registers aggregate-root repositories with explicit read/write context ownership.</summary>
    /// <typeparam name="TEntity">The entity.</typeparam>
    /// <typeparam name="TId">Its identifier.</typeparam>
    /// <typeparam name="TRead">The service's read context.</typeparam>
    /// <typeparam name="TWrite">The service's write context.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The configured registrations for chaining.</returns>
    /// <exception cref="InvalidOperationException">Read and write contexts use the same type.</exception>
    public static IServiceCollection AddRepositories<TEntity, TId, TRead, TWrite>(this IServiceCollection services)
        where TEntity : class, IReadModel<TId>, IAggregateRoot
        where TRead : ScopedDbContext
        where TWrite : ScopedDbContext
    {
        if (typeof(TRead) == typeof(TWrite))
        {
            throw new InvalidOperationException("Register separate read and write context types.");
        }

        _ = services.AddReadRepository<TEntity, TId, TRead>();
        _ = services.AddScoped<IGenericWriteRepository<TEntity, TId>, GenericWriteRepository<TEntity, TId, TWrite>>();
        return services;
    }

    /// <summary>Registers manual EF transactions for code paths without Wolverine message publication.</summary>
    /// <typeparam name="TContext">The service's write context.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The configured registrations for chaining.</returns>
    public static IServiceCollection AddEfUnitOfWork<TContext>(this IServiceCollection services)
        where TContext : ScopedDbContext => services.AddScoped<IUnitOfWork, UnitOfWork<TContext>>();

    /// <summary>Enables Wolverine's EF transaction pipeline and local domain-event collection.</summary>
    /// <typeparam name="TWrite">The concrete service write context.</typeparam>
    /// <param name="options">Wolverine configuration.</param>
    public static void UseFoundationEfTransactions<TWrite>(this WolverineOptions options)
        where TWrite : WriteDbContext
    {
        _ = options.UseEntityFrameworkCoreTransactions(TransactionMiddlewareMode.Eager)
            .WithDbContextAbstraction<IUnitOfWork, TWrite>();
        _ = options.PublishDomainEventsFromEntityFrameworkCore<IHasDomainEvents>(entity => entity.ClearDomainEvents());
        options.Policies.UseDurableLocalQueues();
        options.Policies.AutoApplyTransactions();
        _ = options.Services.AddScoped<IUnitOfWork>(services => services.GetRequiredService<TWrite>());
    }
}
