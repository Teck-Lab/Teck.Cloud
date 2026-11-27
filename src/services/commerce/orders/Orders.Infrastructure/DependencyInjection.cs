using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.Checkout;
using Teck.Cloud.Commerce.Orders.Application.Orders.Cursor;
using Teck.Cloud.Commerce.Orders.Application.Orders.ReadModels;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Baskets;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Idempotency;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.OrderAudit;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.PlacementProbe;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.ProviderEvent;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Reconciliation;
using Teck.Cloud.Commerce.Orders.Infrastructure.Database;
using Teck.Cloud.Commerce.Orders.Infrastructure.Work;

namespace Teck.Cloud.Commerce.Orders.Infrastructure;

/// <summary>Explicit persistence, port and worker-store registrations.</summary>
public static class DependencyInjection
{
    /// <summary>Registers the distinct read/write contexts, repositories, ports and worker stores.</summary>
    /// <param name="services">Host registrations.</param>
    /// <param name="configuration">Host configuration.</param>
    /// <returns>The configured registrations.</returns>
    /// <exception cref="InvalidOperationException">The Orders connection string is not configured.</exception>
    public static IServiceCollection AddOrdersInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var write = configuration.GetConnectionString("orders")
            ?? throw new InvalidOperationException("ConnectionStrings:orders is required.");
        var read = configuration.GetConnectionString("orders-read") ?? write;
        _ = services.AddDbContext<OrdersWriteDbContext>(options => options.UseNpgsql(
            write,
            npgsql => npgsql.MigrationsAssembly(typeof(OrdersWriteDbContext).Assembly.GetName().Name)));
        _ = services.AddDbContext<OrdersReadDbContext>(options => options.UseNpgsql(read));
        _ = services.AddSingleton(new NpgsqlDataSourceBuilder(write).Build());
        _ = services.AddRepositories<Order, Guid, OrdersReadDbContext, OrdersWriteDbContext>();
        _ = services.AddRepositories<Basket, Guid, OrdersReadDbContext, OrdersWriteDbContext>();
        _ = services.AddRepositories<OrderAuditEntry, Guid, OrdersReadDbContext, OrdersWriteDbContext>();
        _ = services.AddRepositories<IdempotencyReceipt, Guid, OrdersReadDbContext, OrdersWriteDbContext>();
        _ = services.AddRepositories<PlacementProbeWork, Guid, OrdersReadDbContext, OrdersWriteDbContext>();
        _ = services.AddRepositories<ProviderEventReceipt, Guid, OrdersReadDbContext, OrdersWriteDbContext>();
        _ = services.AddRepositories<ReconciliationJob, Guid, OrdersReadDbContext, OrdersWriteDbContext>();
        _ = services.AddReadRepository<OrderReadModel, Guid, OrdersReadDbContext>();
        _ = services.AddReadRepository<OrderAuditReadModel, Guid, OrdersReadDbContext>();
        _ = services.AddReadRepository<BasketReadModel, Guid, OrdersReadDbContext>();
        _ = services.AddSingleton<IOrderCursorCodec, HmacOrderCursorCodec>();
        _ = services.AddScoped<ICheckoutIntentLock, AdvisoryCheckoutIntentLock>();
        _ = services.AddScoped<ProbeWorkStore>();
        _ = services.AddScoped<ReconciliationJobStore>();
        _ = services.AddScoped<OrderWorkStore>();
        _ = services.AddScoped<ExpiredEntitlementStore>();
        _ = services.AddScoped<ProviderReceiptStore>();
        _ = services.AddScoped<StripeWebhookIntake>();
        return services;
    }
}
