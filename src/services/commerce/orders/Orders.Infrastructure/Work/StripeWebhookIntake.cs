using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
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
    private const string LockPendingPaymentJobSql =
        "SELECT j.\"Id\"\n" +
        "FROM orders.\"ReconciliationJobs\" AS j\n" +
        "WHERE j.\"TenantId\" = @tenant\n" +
        "  AND j.\"OrderId\" = @order\n" +
        "  AND j.\"Kind\" = @kind\n" +
        "  AND j.\"Status\" = @status\n" +
        "  AND j.\"IsDeleted\" = FALSE\n" +
        "FOR UPDATE;";

    private const string RescheduleLockedJobSql =
        "UPDATE orders.\"ReconciliationJobs\"\n" +
        "SET \"DueAt\" = @now, \"UpdatedOn\" = @now\n" +
        "WHERE \"Id\" = @id AND \"Status\" = @status;";

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
            _ = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return WebhookIntakeOutcome.Accepted;
        }

        receipt.Bind(order.OrderId, order.TenantId);
        var transaction = await context.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await using var transactionLifetime = transaction.ConfigureAwait(false);
        var lockedJobId = await LockPendingPaymentJobAsync(order.TenantId, order.OrderId, cancellationToken).ConfigureAwait(false);
        if (lockedJobId is null)
        {
            _ = context.ReconciliationJobs.Add(
                ReconciliationJob.Create(order.TenantId, order.OrderId, ReconciliationKind.PaymentVerification, now, now, receipt.Id));
        }
        else
        {
            await RescheduleLockedJobAsync(lockedJobId.Value, now, cancellationToken).ConfigureAwait(false);
        }

        _ = context.ProviderEventReceipts.Add(receipt);
        _ = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return WebhookIntakeOutcome.Accepted;
    }

    private async Task<Guid?> LockPendingPaymentJobAsync(string tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        var current = context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Receipt intake requires an explicit transaction.");
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var command = new NpgsqlCommand(LockPendingPaymentJobSql, connection, (NpgsqlTransaction)current.GetDbTransaction());
        await using var commandLifetime = command.ConfigureAwait(false);
        _ = command.Parameters.AddWithValue("tenant", tenantId);
        _ = command.Parameters.AddWithValue("order", orderId);
        _ = command.Parameters.AddWithValue("kind", (int)ReconciliationKind.PaymentVerification);
        _ = command.Parameters.AddWithValue("status", (int)ReconciliationStatus.Pending);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerLifetime = reader.ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? reader.GetGuid(0) : null;
    }

    private async Task RescheduleLockedJobAsync(Guid jobId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var current = context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Receipt intake requires an explicit transaction.");
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var command = new NpgsqlCommand(RescheduleLockedJobSql, connection, (NpgsqlTransaction)current.GetDbTransaction());
        await using var commandLifetime = command.ConfigureAwait(false);
        _ = command.Parameters.AddWithValue("id", jobId);
        _ = command.Parameters.AddWithValue(nameof(now), now);
        _ = command.Parameters.AddWithValue("status", (int)ReconciliationStatus.Pending);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
