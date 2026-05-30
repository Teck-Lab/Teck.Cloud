// <copyright file="AccessPointRegisteredHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.SignalR;
using SharedKernel.Events;
using Statistic.Application.Statistics;
using Statistic.Domain.Statistics;
using Statistic.Infrastructure.Hubs;

namespace Statistic.Infrastructure.Statistics;

/// <summary>
/// Consumes access point registration events and broadcasts updated statistics snapshots.
/// </summary>
public sealed partial class AccessPointRegisteredHandler
{
    private const string DashboardGroup = "dashboard";

    private readonly ISnapshotStore store;
    private readonly IHubContext<StatisticsHub> hubContext;
    private readonly ILogger<AccessPointRegisteredHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessPointRegisteredHandler"/> class.
    /// </summary>
    public AccessPointRegisteredHandler(
        ISnapshotStore store,
        IHubContext<StatisticsHub> hubContext,
        ILogger<AccessPointRegisteredHandler> logger)
    {
        this.store = store;
        this.hubContext = hubContext;
        this.logger = logger;
    }

    /// <summary>
    /// Handles an access point registration integration event.
    /// </summary>
    public async Task Handle(AccessPointRegisteredIntegrationEvent evt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (string.IsNullOrWhiteSpace(evt.LocationNodeId))
        {
            this.logger.LogWarning("Ignoring access point registration without a location node id. SerialNumber: {SerialNumber}", evt.SerialNumber);
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

    private StatSnapshot UpdateAccessPoint(AccessPointRegisteredIntegrationEvent evt)
    {
        StatSnapshot current = this.store.Current;
        List<AccessPointMetric> accessPoints = current.AccessPoints.ToList();
        int accessPointIndex = accessPoints.FindIndex(accessPoint =>
            string.Equals(accessPoint.SerialNumber, evt.SerialNumber, StringComparison.Ordinal));

        AccessPointMetric metric = new(
            SerialNumber: evt.SerialNumber,
            Vendor: evt.Vendor,
            LocationNodeId: evt.LocationNodeId,
            Status: "Registered",
            CurrentLoad: 0,
            MaxCapacity: evt.MaxCapacity);

        if (accessPointIndex < 0)
        {
            accessPoints.Add(metric);
        }
        else
        {
            accessPoints[accessPointIndex] = metric;
        }

        StatSnapshot next = current with { AccessPoints = accessPoints };
        this.store.Update(next);

        AccessPointMetricsUpdated(this.logger, evt.SerialNumber, evt.LocationNodeId);
        return next;
    }

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "Updated access point metrics for serial {SerialNumber} at location {LocationNodeId} after registration event.")]
    private static partial void AccessPointMetricsUpdated(ILogger logger, string serialNumber, string locationNodeId);
}
