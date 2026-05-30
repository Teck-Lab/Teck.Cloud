// <copyright file="DisplayOperationStateChangedHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.SignalR;
using SharedKernel.Events;
using Statistic.Application.Statistics;
using Statistic.Domain.Statistics;
using Statistic.Infrastructure.Hubs;

namespace Statistic.Infrastructure.Statistics;

/// <summary>
/// Consumes display operation state changes and broadcasts updated statistics snapshots.
/// </summary>
public sealed partial class DisplayOperationStateChangedHandler
{
    private const string DashboardGroup = "dashboard";
    private const int MaxTerminalDetailsPerLocation = 50;

    private readonly ISnapshotStore store;
    private readonly IHubContext<StatisticsHub> hubContext;
    private readonly ILogger<DisplayOperationStateChangedHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayOperationStateChangedHandler"/> class.
    /// </summary>
    /// <param name="store">Snapshot store to update.</param>
    /// <param name="hubContext">SignalR hub context for dashboard broadcasts.</param>
    /// <param name="logger">Handler logger.</param>
    public DisplayOperationStateChangedHandler(
        ISnapshotStore store,
        IHubContext<StatisticsHub> hubContext,
        ILogger<DisplayOperationStateChangedHandler> logger)
    {
        this.store = store;
        this.hubContext = hubContext;
        this.logger = logger;
    }

    /// <summary>
    /// Handles a display operation state change from RabbitMQ.
    /// </summary>
    /// <param name="evt">The state changed integration event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(DisplayOperationStateChangedIntegrationEvent evt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (string.IsNullOrWhiteSpace(evt.LocationNodeId))
        {
            this.logger.LogWarning("Ignoring display operation state change without a location node id. DisplayId: {DisplayId}", evt.DisplayId);
            return;
        }

        StatSnapshot next = this.UpdateDisplayJobs(evt);

        DashboardNotification notification = BuildNotification(evt);
        StatSnapshot locationSnapshot = StatisticsHub.FilterSnapshot(next, evt.LocationNodeId);
        string locationGroup = StatisticsHub.BuildLocationGroup(evt.LocationNodeId);

        await this.hubContext.Clients
            .Group(locationGroup)
            .SendAsync("ReceiveSnapshot", locationSnapshot, cancellationToken)
            .ConfigureAwait(false);

        await this.hubContext.Clients
            .Group(locationGroup)
            .SendAsync("ReceiveNotification", notification, cancellationToken)
            .ConfigureAwait(false);

        await this.hubContext.Clients
            .Group(DashboardGroup)
            .SendAsync("ReceiveSnapshot", next, cancellationToken)
            .ConfigureAwait(false);

        await this.hubContext.Clients
            .Group(DashboardGroup)
            .SendAsync("ReceiveNotification", notification, cancellationToken)
            .ConfigureAwait(false);
    }

    private static DashboardNotification BuildNotification(DisplayOperationStateChangedIntegrationEvent evt)
    {
        string level = evt.Status switch
        {
            "Completed" => "success",
            "Failed" => "error",
            "Started" => "info",
            "Queued" => "info",
            _ => "info",
        };

        return new DashboardNotification(
            Id: Guid.NewGuid().ToString("N"),
            Title: $"Display operation {evt.Status.ToLowerInvariant()}",
            Message: $"{evt.OperationType} for display {evt.DisplayId} at location {evt.LocationNodeId} is {evt.Status.ToLowerInvariant()}.",
            Level: level,
            OccurredAt: evt.Timestamp.UtcDateTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static DisplayJobMetric ApplyStateChange(DisplayJobMetric current, DisplayOperationStateChangedIntegrationEvent evt)
    {
        int completedToday = IsSameUtcDay(current.LastUpdated, evt.Timestamp) ? current.CompletedJobsToday : 0;
        int failedToday = IsSameUtcDay(current.LastUpdated, evt.Timestamp) ? current.FailedJobsToday : 0;

        return evt.Status switch
        {
            "Queued" => current with
            {
                PendingJobs = Math.Max(evt.QueueDepth, current.PendingJobs + 1),
                CompletedJobsToday = completedToday,
                FailedJobsToday = failedToday,
                LastUpdated = evt.Timestamp,
            },
            "Started" => current with
            {
                ActiveJobs = current.ActiveJobs + 1,
                PendingJobs = Math.Max(0, current.PendingJobs - 1),
                CompletedJobsToday = completedToday,
                FailedJobsToday = failedToday,
                LastUpdated = evt.Timestamp,
            },
            "Completed" => current with
            {
                ActiveJobs = Math.Max(0, current.ActiveJobs - 1),
                PendingJobs = Math.Max(0, Math.Min(current.PendingJobs, evt.QueueDepth)),
                CompletedJobsToday = completedToday + 1,
                FailedJobsToday = failedToday,
                LastUpdated = evt.Timestamp,
            },
            "Failed" => current with
            {
                ActiveJobs = Math.Max(0, current.ActiveJobs - 1),
                PendingJobs = Math.Max(0, Math.Min(current.PendingJobs, evt.QueueDepth)),
                CompletedJobsToday = completedToday,
                FailedJobsToday = failedToday + 1,
                LastUpdated = evt.Timestamp,
            },
            _ => current with
            {
                PendingJobs = Math.Max(0, evt.QueueDepth),
                CompletedJobsToday = completedToday,
                FailedJobsToday = failedToday,
                LastUpdated = evt.Timestamp,
            },
        };
    }

    private static DisplayJobMetric CreateMetric(DisplayOperationStateChangedIntegrationEvent evt) => new(
        evt.LocationNodeId,
        ActiveJobs: 0,
        PendingJobs: 0,
        CompletedJobsToday: 0,
        FailedJobsToday: 0,
        LastUpdated: evt.Timestamp);

    private static bool IsSameUtcDay(DateTimeOffset left, DateTimeOffset right) => left.UtcDateTime.Date == right.UtcDateTime.Date;

    private static DisplayJobDetail ApplyDetailStateChange(DisplayJobDetail current, DisplayOperationStateChangedIntegrationEvent evt) => current with
    {
        OperationType = evt.OperationType,
        Status = evt.Status,
        StartedAt = evt.Status == "Started" ? evt.Timestamp : current.StartedAt,
        CompletedAt = IsTerminalStatus(evt.Status) ? evt.Timestamp : current.CompletedAt,
        FailureReason = evt.Status == "Failed" ? current.FailureReason : null,
    };

    private static DisplayJobDetail CreateDetail(DisplayOperationStateChangedIntegrationEvent evt) => new(
        evt.LocationNodeId,
        evt.DisplayId,
        evt.OperationType,
        evt.Status,
        RequestedAt: evt.Timestamp,
        StartedAt: evt.Status == "Started" ? evt.Timestamp : null,
        CompletedAt: IsTerminalStatus(evt.Status) ? evt.Timestamp : null,
        FailureReason: null);

    private static bool IsTerminalStatus(string status) => status is "Completed" or "Failed";

    private static List<DisplayJobDetail> TrimTerminalDetails(List<DisplayJobDetail> details)
    {
        HashSet<DisplayJobDetail> terminalDetailsToKeep = details
            .Where(detail => IsTerminalStatus(detail.Status))
            .GroupBy(detail => detail.LocationNodeId, StringComparer.Ordinal)
            .SelectMany(group => group
                .OrderByDescending(detail => detail.CompletedAt ?? detail.RequestedAt)
                .Take(MaxTerminalDetailsPerLocation))
            .ToHashSet();

        return details
            .Where(detail => !IsTerminalStatus(detail.Status) || terminalDetailsToKeep.Contains(detail))
            .ToList();
    }

    private StatSnapshot UpdateDisplayJobs(DisplayOperationStateChangedIntegrationEvent evt)
    {
        StatSnapshot current = this.store.Current;
        List<DisplayJobMetric> displayJobs = current.DisplayJobs.ToList();
        List<DisplayJobDetail> displayJobDetails = current.DisplayJobDetails.ToList();
        int metricIndex = displayJobs.FindIndex(metric => string.Equals(metric.LocationNodeId, evt.LocationNodeId, StringComparison.Ordinal));

        if (metricIndex < 0)
        {
            displayJobs.Add(ApplyStateChange(CreateMetric(evt), evt));
        }
        else
        {
            displayJobs[metricIndex] = ApplyStateChange(displayJobs[metricIndex], evt);
        }

        int detailIndex = displayJobDetails.FindIndex(detail =>
            detail.DisplayId == evt.DisplayId &&
            string.Equals(detail.LocationNodeId, evt.LocationNodeId, StringComparison.Ordinal));

        if (detailIndex < 0)
        {
            displayJobDetails.Add(CreateDetail(evt));
        }
        else
        {
            displayJobDetails[detailIndex] = ApplyDetailStateChange(displayJobDetails[detailIndex], evt);
        }

        displayJobDetails = TrimTerminalDetails(displayJobDetails);

        StatSnapshot next = current with { DisplayJobs = displayJobs, DisplayJobDetails = displayJobDetails };
        this.store.Update(next);

        DisplayJobMetricsUpdated(this.logger, evt.LocationNodeId, evt.Status);

        return next;
    }

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Updated display job metrics for location {LocationNodeId} after {Status} operation event.")]
    private static partial void DisplayJobMetricsUpdated(ILogger logger, string locationNodeId, string status);
}
