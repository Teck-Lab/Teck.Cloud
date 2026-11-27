using ErrorOr;
using Microsoft.Extensions.Options;
using Teck.Cloud.Commerce.Orders.Application.Orders.Billing;
using Teck.Cloud.Commerce.Orders.Application.Orders.Features.ApplyPaymentSnapshot.V1;
using Teck.Cloud.Commerce.Orders.Application.Orders.Features.CheckoutWork.V1;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Application.Orders.Settings;
using Teck.Cloud.Commerce.Orders.Domain.Common;
using Teck.Cloud.Commerce.Orders.Infrastructure.Work;
using Wolverine;

namespace Teck.Cloud.Commerce.Orders.Host.Workers;

/// <summary>Leased provider work runner; network calls never run inside a database transaction.</summary>
/// <param name="scopeFactory">The scoped service factory.</param>
/// <param name="clock">The application clock.</param>
/// <param name="options">The provider work bounds.</param>
/// <param name="logger">The worker logger.</param>
public sealed partial class ProviderReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    IOptions<ProviderWorkOptions> options,
    ILogger<ProviderReconciliationWorker> logger) : BackgroundService
{
    private int RetrySeconds => Math.Max(1, options.Value.RetrySeconds);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.ScanIntervalSeconds));
        using var timer = new PeriodicTimer(interval, clock);
        try
        {
            do
            {
                try
                {
                    await ScanAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogScanFailed(logger, exception);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // Host shutdown.
        }
    }

    private static async Task CompleteSessionAsync(
        JobClaim claim,
        IMessageBus bus,
        ProviderReceiptStore receipts,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        _ = await bus.InvokeForTenantAsync<ErrorOr<bool>>(
            claim.TenantId,
            new CompleteReconciliationJobCommand(claim.TenantId, claim.OrderId, claim.JobId, claim.LeaseToken),
            cancellationToken).ConfigureAwait(false);
        await CompleteReceiptAsync(claim, receipts, now, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ApplySessionAsync(
        JobClaim claim,
        IMessageBus bus,
        BillingCheckout checkout,
        string session,
        string url,
        ProviderReceiptStore receipts,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var applied = await bus.InvokeForTenantAsync<ErrorOr<bool>>(
            claim.TenantId,
            new ApplyCheckoutSessionCommand(
                claim.TenantId,
                claim.OrderId,
                claim.JobId,
                claim.LeaseToken,
                session,
                url,
                Mode(checkout.Mode),
                checkout.AccountId),
            cancellationToken).ConfigureAwait(false);
        if (applied.IsError || !applied.Value)
        {
            return;
        }

        await CompleteReceiptAsync(claim, receipts, now, cancellationToken).ConfigureAwait(false);
    }

    private static async Task CompleteReceiptAsync(
        JobClaim claim,
        ProviderReceiptStore receipts,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (claim.ProviderEventReceiptId is { } receiptId)
        {
            await receipts.MarkCompletedAsync(receiptId, now, cancellationToken).ConfigureAwait(false);
        }
    }

    private static BillingOrder BuildOrder(OrderWorkFact facts, IReadOnlyList<BillingVerifiedPriceLine> lines) => new(
        Guid.Parse(facts.TenantId),
        facts.OrderId,
        facts.SubscriptionId,
        facts.OfferId,
        TierName(facts.Tier),
        lines,
        facts.SessionId,
        facts.ProviderSubscriptionId,
        facts.CheckoutRequestedAt);

    private static string TierName(int tier) => tier == (int)Tier.Dedicated ? "dedicated" : "shared";

    private static string Mode(BillingProviderMode mode) => mode == BillingProviderMode.Live ? "live" : "test";

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "The provider reconciliation scan failed; the next tick retries.")]
    private static partial void LogScanFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Provider work failed; the lease expiry retries the work.")]
    private static partial void LogWorkFailed(ILogger logger, Exception exception);

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        var scope = scopeFactory.CreateAsyncScope();
        try
        {
            var provider = scope.ServiceProvider;
            var now = clock.GetUtcNow();
            var token = Guid.NewGuid();
            var leaseUntil = now.AddSeconds(Math.Max(1, options.Value.LeaseSeconds));
            var claims = await provider.GetRequiredService<ReconciliationJobStore>()
                .ClaimAsync(now, token, leaseUntil, Math.Clamp(options.Value.BatchSize, 1, 20), cancellationToken)
                .ConfigureAwait(false);
            var bus = provider.GetRequiredService<IMessageBus>();
            var billing = provider.GetRequiredService<ICommerceBilling>();
            var orders = provider.GetRequiredService<OrderWorkStore>();
            var receipts = provider.GetRequiredService<ProviderReceiptStore>();
            foreach (var claim in claims)
            {
                try
                {
                    await ProcessAsync(claim, bus, billing, orders, receipts, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogWorkFailed(logger, exception);
                }
            }
        }
        finally
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task ProcessAsync(
        JobClaim claim,
        IMessageBus bus,
        ICommerceBilling billing,
        OrderWorkStore orders,
        ProviderReceiptStore receipts,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var facts = await orders.LoadAsync(claim.TenantId, claim.OrderId, cancellationToken).ConfigureAwait(false);
        if (facts is null)
        {
            await FailAsync(bus, claim, "order_not_found", now.AddHours(1), abandon: true, receipts, cancellationToken).ConfigureAwait(false);
            return;
        }

        switch (claim.Kind)
        {
            case ReconciliationKind.CheckoutSession:
                await ProcessCheckoutAsync(claim, facts, bus, billing, receipts, now, cancellationToken).ConfigureAwait(false);
                break;
            case ReconciliationKind.PaymentVerification:
            case ReconciliationKind.PaymentRenewalCheck:
                await ProcessPaymentAsync(claim, facts, bus, billing, receipts, now, cancellationToken).ConfigureAwait(false);
                break;
            default:
                await FailAsync(bus, claim, "unsupported_work", now.AddHours(1), abandon: true, receipts, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task ProcessCheckoutAsync(
        JobClaim claim,
        OrderWorkFact facts,
        IMessageBus bus,
        ICommerceBilling billing,
        ProviderReceiptStore receipts,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (facts.SessionId is not null)
        {
            await CompleteSessionAsync(claim, bus, receipts, now, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (facts.CheckoutCreateAttempts > 0
            && now > facts.CheckoutRequestedAt.AddHours(Math.Max(1, options.Value.AmbiguityHours)))
        {
            await FailAsync(bus, claim, "checkout_ambiguous", now.AddHours(1), abandon: true, receipts, cancellationToken).ConfigureAwait(false);
            return;
        }

        var verification = await VerifyPriceAsync(claim, facts, bus, billing, receipts, now, cancellationToken)
            .ConfigureAwait(false);
        if (verification is null)
        {
            return;
        }

        await StartCheckoutAsync(claim, facts, bus, billing, receipts, verification, now, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<BillingPriceVerification?> VerifyPriceAsync(
        JobClaim claim,
        OrderWorkFact facts,
        IMessageBus bus,
        ICommerceBilling billing,
        ProviderReceiptStore receipts,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var catalog = PriceSnapshot.DecodeCatalog(facts.CatalogPriceSnapshot);
        if (catalog is null)
        {
            await FailAsync(bus, claim, "invalid_configuration", now.AddSeconds(RetrySeconds), abandon: false, receipts, cancellationToken).ConfigureAwait(false);
            return null;
        }

        BillingPriceVerification verification;
        try
        {
            verification = await billing.ValidatePriceAsync(catalog, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _ = exception;
            await FailAsync(bus, claim, "provider_unavailable", now.AddSeconds(RetrySeconds), abandon: false, receipts, cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (verification.Status == BillingPriceVerificationStatus.Verified)
        {
            return verification;
        }

        await FailAsync(
            bus,
            claim,
            verification.FailureCode ?? "provider_unavailable",
            now.AddSeconds(RetrySeconds),
            abandon: false,
            receipts,
            cancellationToken).ConfigureAwait(false);
        return null;
    }

    private async Task StartCheckoutAsync(
        JobClaim claim,
        OrderWorkFact facts,
        IMessageBus bus,
        ICommerceBilling billing,
        ProviderReceiptStore receipts,
        BillingPriceVerification verification,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var began = await bus.InvokeForTenantAsync<ErrorOr<bool>>(
            claim.TenantId,
            new BeginCheckoutAttemptCommand(
                claim.TenantId,
                claim.OrderId,
                claim.JobId,
                claim.LeaseToken,
                verification.Lines),
            cancellationToken).ConfigureAwait(false);
        if (began.IsError || !began.Value)
        {
            return;
        }

        var order = BuildOrder(facts, verification.Lines);
        BillingCheckout checkout;
        try
        {
            checkout = facts.CheckoutCreateAttempts == 0
                ? await billing.CreateAsync(order, cancellationToken).ConfigureAwait(false)
                : await billing.RecoverAsync(order, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _ = exception;
            await FailAsync(bus, claim, "provider_unavailable", now.AddSeconds(RetrySeconds), abandon: false, receipts, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (checkout is { Status: BillingCheckoutStatus.Created, ProviderSessionId: { } session, CheckoutUrl: { } url })
        {
            await ApplySessionAsync(claim, bus, checkout, session, url, receipts, now, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await FailCheckoutAsync(claim, bus, checkout, receipts, now, cancellationToken).ConfigureAwait(false);
    }

    private Task FailCheckoutAsync(
        JobClaim claim,
        IMessageBus bus,
        BillingCheckout checkout,
        ProviderReceiptStore receipts,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        FailAsync(
            bus,
            claim,
            checkout.FailureCode ?? "checkout_unavailable",
            now.AddSeconds(RetrySeconds),
            abandon: false,
            receipts,
            cancellationToken);

    private async Task ProcessPaymentAsync(
        JobClaim claim,
        OrderWorkFact facts,
        IMessageBus bus,
        ICommerceBilling billing,
        ProviderReceiptStore receipts,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var order = BuildOrder(facts, PriceSnapshot.Decode(facts.VerifiedPriceSnapshot));
        BillingReconciliation snapshot;
        try
        {
            snapshot = await billing.ReconcileAsync(order, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _ = exception;
            await FailAsync(bus, claim, "provider_unavailable", now.AddSeconds(RetrySeconds), abandon: false, receipts, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (snapshot.Status is not (BillingEvidenceStatus.Paid
            or BillingEvidenceStatus.Refunded
            or BillingEvidenceStatus.Disputed
            or BillingEvidenceStatus.Canceled))
        {
            var category = snapshot.Status switch
            {
                BillingEvidenceStatus.None => snapshot.FailureCode ?? "provider_unavailable",
                BillingEvidenceStatus.Pending => snapshot.FailureCode ?? "provider_unavailable",
                BillingEvidenceStatus.Paid => snapshot.FailureCode ?? "provider_unavailable",
                BillingEvidenceStatus.Refunded => snapshot.FailureCode ?? "provider_unavailable",
                BillingEvidenceStatus.Disputed => snapshot.FailureCode ?? "provider_unavailable",
                BillingEvidenceStatus.Canceled => snapshot.FailureCode ?? "provider_unavailable",
                BillingEvidenceStatus.Mismatch => "binding_mismatch",
                BillingEvidenceStatus.Unavailable => "provider_unavailable",
                _ => snapshot.FailureCode ?? "provider_unavailable",
            };
            await FailAsync(bus, claim, category, now.AddSeconds(RetrySeconds), abandon: false, receipts, cancellationToken).ConfigureAwait(false);
            return;
        }

        var applied = await bus.InvokeForTenantAsync<ErrorOr<bool>>(
            claim.TenantId,
            new ApplyPaymentSnapshotCommand(claim.TenantId, claim.OrderId, claim.JobId, claim.LeaseToken, snapshot),
            cancellationToken).ConfigureAwait(false);
        if (applied.IsError || !applied.Value)
        {
            await FailAsync(bus, claim, "binding_mismatch", now.AddSeconds(RetrySeconds), abandon: false, receipts, cancellationToken).ConfigureAwait(false);
            return;
        }

        await CompleteReceiptAsync(claim, receipts, now, cancellationToken).ConfigureAwait(false);
    }

    private async Task FailAsync(
        IMessageBus bus,
        JobClaim claim,
        string category,
        DateTimeOffset nextDue,
        bool abandon,
        ProviderReceiptStore receipts,
        CancellationToken cancellationToken)
    {
        _ = await bus.InvokeForTenantAsync<ErrorOr<bool>>(
            claim.TenantId,
            new RecordReconciliationFailureCommand(
                claim.TenantId,
                claim.OrderId,
                claim.JobId,
                claim.LeaseToken,
                category,
                nextDue,
                abandon),
            cancellationToken).ConfigureAwait(false);
        if (claim.ProviderEventReceiptId is { } receiptId)
        {
            await receipts.MarkFailedAsync(receiptId, category, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }
    }
}
