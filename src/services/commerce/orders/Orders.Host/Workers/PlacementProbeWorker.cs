using ErrorOr;
using Microsoft.Extensions.Options;
using Teck.Cloud.Commerce.Orders.Application.Orders.Features.ApplyProbeOutcome.V1;
using Teck.Cloud.Commerce.Orders.Application.Orders.Features.ReconcileEntitlement.V1;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Application.Orders.Settings;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Baskets;
using Teck.Cloud.Commerce.Orders.Domain.Common;
using Teck.Cloud.Commerce.Orders.Infrastructure.Work;
using Wolverine;

namespace Teck.Cloud.Commerce.Orders.Host.Workers;

/// <summary>The single bounded, leased placement-probe scheduler.</summary>
/// <param name="scopeFactory">The scoped service factory.</param>
/// <param name="clock">The application clock.</param>
/// <param name="options">The probe bounds.</param>
/// <param name="logger">The worker logger.</param>
public sealed partial class PlacementProbeWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    IOptions<PlacementProbeOptions> options,
    ILogger<PlacementProbeWorker> logger) : BackgroundService
{
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

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "The placement probe scan failed; the next tick retries.")]
    private static partial void LogScanFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Expired entitlement reconciliation failed for one order.")]
    private static partial void LogExpiredEntitlementFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "A placement probe failed transiently.")]
    private static partial void LogProbeFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "A probe outcome application failed; the lease expires and retries.")]
    private static partial void LogOutcomeFailed(ILogger logger, Exception exception);

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        var scope = scopeFactory.CreateAsyncScope();
        try
        {
            var provider = scope.ServiceProvider;
            var bus = provider.GetRequiredService<IMessageBus>();
            var now = clock.GetUtcNow();
            var expired = await provider.GetRequiredService<ExpiredEntitlementStore>()
                .ListDueAsync(now, 50, cancellationToken).ConfigureAwait(false);
            foreach (var (tenantId, orderId) in expired)
            {
                try
                {
                    _ = await bus.InvokeForTenantAsync<ErrorOr<bool>>(
                        tenantId,
                        new ReconcileEntitlementCommand(tenantId, orderId),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogExpiredEntitlementFailed(logger, exception);
                }
            }

            var token = Guid.NewGuid();
            var leaseUntil = now.AddSeconds(Math.Max(1, options.Value.LeaseSeconds));
            var claims = await provider.GetRequiredService<ProbeWorkStore>()
                .ClaimAsync(now, token, leaseUntil, Math.Clamp(options.Value.BatchSize, 1, 25), cancellationToken)
                .ConfigureAwait(false);
            if (claims.Count == 0)
            {
                return;
            }

            var placement = provider.GetRequiredService<IPlacementProbe>();
            await Parallel.ForEachAsync(
                claims,
                new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken },
                async (claim, itemToken) => await ProbeAsync(claim, placement, bus, now, token, itemToken).ConfigureAwait(false))
                .ConfigureAwait(false);
        }
        finally
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task ProbeAsync(
        ProbeWorkClaim claim,
        IPlacementProbe placement,
        IMessageBus bus,
        DateTimeOffset now,
        Guid leaseToken,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(claim.TenantId, "D", out _))
        {
            return;
        }

        var tier = claim.Tier == (int)Tier.Dedicated ? Tier.Dedicated : Tier.Shared;
        var request = new PlacementProbeRequest(
            Guid.Parse(claim.TenantId),
            claim.OrderId,
            claim.SubscriptionId,
            claim.PlacementId,
            tier == Tier.Dedicated ? "dedicated" : "shared",
            BasketSelection.DecodeAddOnCodes(claim.AddOnCodesCsv));
        var (kind, observedAt) = await RunProbeAsync(placement, request, now, cancellationToken).ConfigureAwait(false);
        await ApplyAsync(claim, bus, kind, observedAt, NextDue(kind, observedAt, now), leaseToken, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(ProbeOutcomeKind Kind, DateTimeOffset ObservedAt)> RunProbeAsync(
        IPlacementProbe placement,
        PlacementProbeRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.Value.ProbeTimeoutSeconds)));
        try
        {
            var result = await placement.VerifyAsync(request, timeout.Token).ConfigureAwait(false);
            var kind = result.Status switch
            {
                PlacementProbeStatus.Verified => ProbeOutcomeKind.Verified,
                PlacementProbeStatus.Unavailable => ProbeOutcomeKind.Unavailable,
                PlacementProbeStatus.BindingMismatch => ProbeOutcomeKind.BindingMismatch,
                PlacementProbeStatus.CapabilityUnavailable => ProbeOutcomeKind.CapabilityUnavailable,
                _ => ProbeOutcomeKind.Unavailable,
            };
            return (kind, result.ObservedAt <= now ? result.ObservedAt : now);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogProbeFailed(logger, exception);
            return (ProbeOutcomeKind.Unavailable, clock.GetUtcNow());
        }
    }

    private DateTimeOffset NextDue(ProbeOutcomeKind kind, DateTimeOffset observedAt, DateTimeOffset now) => kind switch
    {
        ProbeOutcomeKind.Verified => observedAt.AddSeconds(Math.Max(1, options.Value.RefreshSeconds)),
        ProbeOutcomeKind.Unavailable => now.AddSeconds(Math.Max(1, options.Value.RetrySeconds)),
        ProbeOutcomeKind.BindingMismatch => now,
        ProbeOutcomeKind.CapabilityUnavailable => now,
        _ => now,
    };

    private async Task ApplyAsync(
        ProbeWorkClaim claim,
        IMessageBus bus,
        ProbeOutcomeKind kind,
        DateTimeOffset observedAt,
        DateTimeOffset nextDue,
        Guid leaseToken,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await bus.InvokeForTenantAsync<ErrorOr<bool>>(
                claim.TenantId,
                new ApplyProbeOutcomeCommand(
                    claim.TenantId,
                    claim.OrderId,
                    claim.WorkId,
                    leaseToken,
                    claim.EvidenceRevision,
                    claim.PlacementId,
                    kind,
                    observedAt,
                    nextDue),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogOutcomeFailed(logger, exception);
        }
    }
}
