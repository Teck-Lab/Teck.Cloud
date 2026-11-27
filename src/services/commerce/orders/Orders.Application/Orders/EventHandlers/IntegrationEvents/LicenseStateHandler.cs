using SharedKernel.Core.Database;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.EventHandlers.IntegrationEvents;

/// <summary>Applies a Licensing outcome to the current desired generation only.</summary>
public static class LicenseStateHandler
{
    /// <summary>Applies the durable outcome through the service inbox and generation guard.</summary>
    /// <param name="message">The licensing outcome.</param>
    /// <param name="orders">The tracked order repository.</param>
    /// <param name="operation">The scoped operation identity.</param>
    /// <param name="directory">The authoritative active-tenant lookup.</param>
    /// <param name="unitOfWork">The save boundary.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Delivery cancellation.</param>
    /// <returns>The delivery task.</returns>
    /// <exception cref="InvalidOperationException">The message tenant is missing or is not active.</exception>
    public static Task HandleAsync(
        LicenseStateEvent message,
        IGenericWriteRepository<Order, Guid> orders,
        OperationContext operation,
        ITenantDirectory directory,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return ApplyStateAsync(message, orders, operation, directory, unitOfWork, clock, cancellationToken);
    }

    private static async Task ApplyStateAsync(
        LicenseStateEvent message,
        IGenericWriteRepository<Order, Guid> orders,
        OperationContext operation,
        ITenantDirectory directory,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (message.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("A LicenseStateEvent requires a canonical tenant id.");
        }

        var tenant = message.TenantId.ToString("D");
        if (!await directory.IsActiveAsync(tenant, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The message tenant is not active.");
        }

        operation.Initialize(DataScope.ForTenant(tenant));
        var order = await orders.LoadForUpdateAsync(message.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null
            || order.SubscriptionId != message.SubscriptionId
            || order.DesiredRevision != message.DesiredRevision)
        {
            return;
        }

        var state = message.State switch
        {
            "active" => LicenseState.Active,
            "revoked" => LicenseState.Revoked,
            _ => LicenseState.Inactive,
        };
        var applied = order.ApplyLicenseOutcome(
            message.DesiredRevision,
            message.SubscriptionId,
            state,
            message.LicenseRevision,
            message.KeyId,
            message.ExpiresAt,
            clock.GetUtcNow());
        if (applied)
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
