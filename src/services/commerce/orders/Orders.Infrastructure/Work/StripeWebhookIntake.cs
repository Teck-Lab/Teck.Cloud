using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.ProviderEvent;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Reconciliation;
using Teck.Cloud.Commerce.Orders.Domain.Common;
using Teck.Cloud.Commerce.Orders.Infrastructure.Database;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Work;

/// <summary>Verifies signed provider events and durably records receipts and work before acknowledgement.</summary>
/// <param name="billing">The signature-verifying provider adapter.</param>
/// <param name="context">The global-scope write context.</param>
/// <param name="orders">The provider-reference order resolver.</param>
/// <param name="clock">The application clock.</param>
public sealed class StripeWebhookIntake(
    ICommerceBilling billing,
    OrdersWriteDbContext context,
    OrderWorkStore orders,
    TimeProvider clock)
{
    /// <summary>Verifies and persists one provider event.</summary>
    /// <param name="rawBody">The exact raw request body.</param>
    /// <param name="signatureHeader">The timestamped signature header.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The acknowledgement outcome.</returns>
    public Task<WebhookIntakeOutcome> HandleAsync(string rawBody, string signatureHeader, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rawBody);
        ArgumentNullException.ThrowIfNull(signatureHeader);
        return HandleCoreAsync(rawBody, signatureHeader, cancellationToken);
    }

    private async Task<WebhookIntakeOutcome> HandleCoreAsync(string rawBody, string signatureHeader, CancellationToken cancellationToken)
    {
        var notice = billing.ParseWebhook(rawBody, signatureHeader);
        if (notice is null)
        {
            return WebhookIntakeOutcome.InvalidSignature;
        }

        var account = string.IsNullOrWhiteSpace(notice.AccountId) ? "unconfigured" : notice.AccountId;
        if (await context.ProviderEventReceipts.AsNoTracking()
            .AnyAsync(receipt => receipt.AccountId == account && receipt.EventId == notice.EventId, cancellationToken)
            .ConfigureAwait(false))
        {
            return WebhookIntakeOutcome.Duplicate;
        }

        var now = clock.GetUtcNow();
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody)));
        var receipt = ProviderEventReceipt.Create(
            account,
            notice.EventId,
            notice.EventType,
            hash,
            notice.OrderId,
            tenantId: null,
            now);
        var order = await orders.FindByProviderAsync(notice, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            receipt.MarkUnmatched(now);
            _ = context.ProviderEventReceipts.Add(receipt);
            _ = await SaveAsync(cancellationToken).ConfigureAwait(false);
            return WebhookIntakeOutcome.Accepted;
        }

        receipt.Bind(order.OrderId, order.TenantId);
        var job = await context.ReconciliationJobs.AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.TenantId == order.TenantId
                    && candidate.OrderId == order.OrderId
                    && candidate.Kind == ReconciliationKind.PaymentVerification
                    && candidate.Status == ReconciliationStatus.Pending,
                cancellationToken)
            .ConfigureAwait(false);
        if (job is null)
        {
            _ = context.ReconciliationJobs.Add(
                ReconciliationJob.Create(order.TenantId, order.OrderId, ReconciliationKind.PaymentVerification, now, now, receipt.Id));
        }
        else
        {
            _ = job.Reschedule(now, now);
            _ = context.ReconciliationJobs.Update(job);
        }

        _ = context.ProviderEventReceipts.Add(receipt);
        _ = await SaveAsync(cancellationToken).ConfigureAwait(false);
        return WebhookIntakeOutcome.Accepted;
    }

    private async Task<int> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            return 0;
        }
    }
}
