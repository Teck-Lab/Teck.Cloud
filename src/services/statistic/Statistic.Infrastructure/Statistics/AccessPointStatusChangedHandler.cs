// <copyright file="AccessPointStatusChangedHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.SignalR;
using SharedKernel.Events;
using Statistic.Application.Statistics;
using Statistic.Domain.Statistics;
using Statistic.Infrastructure.Hubs;

namespace Statistic.Infrastructure.Statistics;

/// <summary>
/// Consumes access point status changes and broadcasts updated statistics snapshots.
/// </summary>
public sealed partial class AccessPointStatusChangedHandler
{
    private const string DashboardGroup = "dashboard";

    private readonly ISnapshotStore store;
    private readonly IHubContext<StatisticsHub> hubContext;
    private readonly ILogger<AccessPointStatusChangedHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessPointStatusChangedHandler"/> class.
    /// </summary>
    public AccessPointStatusChangedHandler(
        ISnapshotStore store,
        IHubContext<StatisticsHub> hubContext,
        ILogger<AccessPointStatusChangedHandler> logger)
    {
        this.store = store;
        this.hubContext = hubContext;
        this.logger = logger;
    }

    /// <summary>
    /// Handles an access point status changed integration event.
    /// </summary>
    public async Task Handle(AccessPointStatusChangedIntegrationEvent evt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (string.IsNullOrWhiteSpace(evt.LocationNodeId))
        {
            this.logger.LogWarning("Ignoring access point status changed event without a location node id. SerialNumber: {SerialNumber}", evt.SerialNumber);
            return;
        }

        StatSnapshot next = this.UpdateAccessPoint(evt);
        StatSnapshot locationSnapshot = StatisticsHub.FilterSnapshot(next, evt.LocationNodeId);
        string locationGroup = StatisticsHub.BuildLocationGroup(evt.LocationNodeId);

        await this.hubContext.Clients
            .Group(locationGroup)
            .SendAsync("ReceiveSnapshot", locationSnapshot, cancellationToken)
            .ConfigureAwait(false);

        await this.hubContext.Clients
            .Group(DashboardGroup)
            .SendAsync("ReceiveSnapshot", next, cancellationToken)
            .ConfigureAwait(false);
    }

    private StatSnapshot UpdateAccessPoint(AccessPointStatusChangedIntegrationEvent evt)
    {
        StatSnapshot current = this.store.Current;
        List<AccessPointMetric> accessPoints = current.AccessPoints.ToList();
        int accessPointIndex = accessPoints.FindIndex(accessPoint =>
            string.Equals(accessPoint.SerialNumber, evt.SerialNumber, StringComparison.Ordinal));

        if (accessPointIndex < 0)
        {
            accessPoints.Add(new AccessPointMetric(
                SerialNumber: evt.SerialNumber,
                Vendor: string.Empty,
                LocationNodeId: evt.LocationNodeId,
                Status: evt.NewStatus,
                CurrentLoad: 0,
                MaxCapacity: 0));
        }
        else
        {
            AccessPointMetric existing = accessPoints[accessPointIndex];
            accessPoints[accessPointIndex] = existing with
            {
                LocationNodeId = evt.LocationNodeId,
                Status = evt.NewStatus,
            };
        }

        StatSnapshot next = current with { AccessPoints = accessPoints };
        this.store.Update(next);

        AccessPointMetricsUpdated(this.logger, evt.SerialNumber, evt.LocationNodeId, evt.NewStatus);
        return next;
    }

    [LoggerMessage(
        EventId = 1103,
        Level = LogLevel.Information,
        Message = "Updated access point metrics for serial {SerialNumber} at location {LocationNodeId} after status changed to {Status}.")]
    private static partial void AccessPointMetricsUpdated(ILogger logger, string serialNumber, string locationNodeId, string status);
}
