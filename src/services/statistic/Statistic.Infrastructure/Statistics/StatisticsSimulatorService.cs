// <copyright file="StatisticsSimulatorService.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Statistic.Application.Statistics;
using Statistic.Domain.Statistics;
using Statistic.Infrastructure.Hubs;
namespace Statistic.Infrastructure.Statistics;

/// <summary>
/// Background service that mutates the current snapshot randomly every two seconds
/// and broadcasts it to all clients in the "dashboard" group via SignalR.
/// </summary>
internal sealed class StatisticsSimulatorService(
    ISnapshotStore store,
    IHubContext<StatisticsHub> hubContext,
    ILogger<StatisticsSimulatorService> logger) : BackgroundService
{
    private static readonly string[] ActivityTemplates =
    [
        "Order #{0} placed by {1}",
        "Tenant {1} updated billing info",
        "Label batch exported — {2} items",
        "New product added to {1} catalogue",
        "Payment confirmed for order #{0}",
    ];

    private static readonly string[] Tenants = ["Acme Corp", "Globex", "Initech", "Umbrella", "Hooli"];

    private static readonly (string Level, string Title, string Message)[] NotificationTemplates =
    [
        ("success", "Order fulfilled", "Order #{0} has been shipped to {1}."),
        ("info",    "New tenant onboarded", "{1} has completed onboarding."),
        ("warning", "Low stock alert", "Product SKU-{2} has fewer than 10 units remaining."),
        ("error",   "Payment failed", "Order #{0} payment declined — action required."),
        ("success", "Label batch complete", "Batch of {2} labels exported successfully."),
        ("info",    "Billing updated", "{1} updated their billing information."),
        ("warning", "Unusual activity", "Spike in order volume detected for {1}."),
    ];

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Statistics simulator started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);

                StatSnapshot next = Mutate(store.Current);
                store.Update(next);

                await hubContext.Clients
                    .Group("dashboard")
                    .SendAsync("ReceiveSnapshot", next, stoppingToken)
                    .ConfigureAwait(false);

                // Emit a push notification on every tick while testing (restore to ~1-in-5 later)
                try
                {
                    DashboardNotification notification = BuildNotification();
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug("Sending ReceiveNotification: {Level} – {Title}", notification.Level, notification.Title);
                    }

                    await hubContext.Clients
                        .Group("dashboard")
                        .SendAsync("ReceiveNotification", notification, stoppingToken)
                        .ConfigureAwait(false);

                    logger.LogDebug("ReceiveNotification sent successfully.");
                }
                catch (Exception notificationException)
                {
                    logger.LogError(notificationException, "Failed to send ReceiveNotification.");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error broadcasting statistics snapshot.");
            }
        }

        logger.LogInformation("Statistics simulator stopped.");
    }

    private static DashboardNotification BuildNotification()
    {
        var (level, titleTemplate, messageTemplate) =
            NotificationTemplates[RandomNumberGenerator.GetInt32(NotificationTemplates.Length)];

        string tenant = Tenants[RandomNumberGenerator.GetInt32(Tenants.Length)];
        int orderId = RandomNumberGenerator.GetInt32(4800, 9999);
        int quantity = RandomNumberGenerator.GetInt32(50, 500);

        string title = string.Format(System.Globalization.CultureInfo.InvariantCulture, titleTemplate, orderId, tenant, quantity);
        string message = string.Format(System.Globalization.CultureInfo.InvariantCulture, messageTemplate, orderId, tenant, quantity);

        return new DashboardNotification(
            Id: Guid.NewGuid().ToString("N"),
            Title: title,
            Message: message,
            Level: level,
            OccurredAt: DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static StatSnapshot Mutate(StatSnapshot current)
    {
        // Nudge monthly data slightly
        var monthly = current.MonthlyData
            .Select(metric => metric with
            {
                Orders = Math.Max(0, metric.Orders + RandomNumberGenerator.GetInt32(-10, 15)),
                Revenue = Math.Max(0, metric.Revenue + RandomNumberGenerator.GetInt32(-500, 800)),
            })
            .ToList();

        // Nudge tenant orders
        var tenants = current.TenantData
            .Select(tenantMetric => tenantMetric with { Orders = Math.Max(0, tenantMetric.Orders + RandomNumberGenerator.GetInt32(-3, 6)) })
            .ToList();

        // Nudge pie values
        var pie = current.PieData
            .Select(pieMetric => pieMetric with { Value = Math.Max(0, pieMetric.Value + RandomNumberGenerator.GetInt32(-20, 30)) })
            .ToList();

        // Prepend a new activity event occasionally
        var activity = current.RecentActivity.ToList();
        if (RandomNumberGenerator.GetInt32(0, 5) == 0)
        {
            string tenant = Tenants[RandomNumberGenerator.GetInt32(Tenants.Length)];
            string template = ActivityTemplates[RandomNumberGenerator.GetInt32(ActivityTemplates.Length)];
            string text = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                template,
                RandomNumberGenerator.GetInt32(4800, 9999),
                tenant,
                RandomNumberGenerator.GetInt32(50, 500));

            activity.Insert(0, new ActivityEvent("just now", text));
            if (activity.Count > 8)
            {
                activity.RemoveAt(activity.Count - 1);
            }
        }

        return new StatSnapshot(monthly, tenants, pie, activity, current.DisplayJobs, current.DisplayJobDetails, current.AccessPoints);
    }
}
