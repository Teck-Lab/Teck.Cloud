// <copyright file="AccessPointLoadChangedHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.SignalR;
using SharedKernel.Events;
using Statistic.Application.Statistics;
using Statistic.Domain.Statistics;
using Statistic.Infrastructure.Hubs;

namespace Statistic.Infrastructure.Statistics;

/// <summary>
/// Consumes access point load changes and broadcasts updated statistics snapshots.
/// </summary>
public sealed partial class AccessPointLoadChangedHandler
{
    private const string DashboardGroup = "dashboard";

    private readonly ISnapshotStore store;
    private readonly IHubContext<StatisticsHub> hubContext;
    private readonly ILogger<AccessPointLoadChangedHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessPointLoadChangedHandler"/> class.
    /// </summary>
    public AccessPointLoadChangedHandler(
        ISnapshotStore store,
        IHubContext<StatisticsHub> hubContext,
        ILogger<AccessPointLoadChangedHandler> logger)
    {
        this.store = store;
        this.hubContext = hubContext;
        this.logger = logger;
    }

    /// <summary>
    /// Handles an access point load changed integration event.
    /// </summary>
    public async Task Handle(AccessPointLoadChangedIntegrationEvent evt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (string.IsNullOrWhiteSpace(evt.LocationNodeId))
        {
            this.logger.LogWarning("Ignoring access point load changed event without a location node id. SerialNumber: {SerialNumber}", evt.SerialNumber);
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

    private StatSnapshot UpdateAccessPoint(AccessPointLoadChangedIntegrationEvent evt)
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
                Status: "Unknown",
                CurrentLoad: evt.NewLoad,
                MaxCapacity: evt.MaxCapacity));
        }
        else
        {
            AccessPointMetric existing = accessPoints[accessPointIndex];
            accessPoints[accessPointIndex] = existing with
            {
                LocationNodeId = evt.LocationNodeId,
                CurrentLoad = evt.NewLoad,
                MaxCapacity = evt.MaxCapacity,
            };
        }

        StatSnapshot next = current with { AccessPoints = accessPoints };
        this.store.Update(next);

        AccessPointMetricsUpdated(this.logger, evt.SerialNumber, evt.LocationNodeId);
        return next;
    }

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Information,
        Message = "Updated access point metrics for serial {SerialNumber} at location {LocationNodeId} after load changed event.")]
    private static partial void AccessPointMetricsUpdated(ILogger logger, string serialNumber, string locationNodeId);
}
