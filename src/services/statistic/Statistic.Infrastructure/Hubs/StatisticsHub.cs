// <copyright file="StatisticsHub.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Statistic.Application.Statistics;
using Statistic.Domain.Statistics;

namespace Statistic.Infrastructure.Hubs;

/// <summary>
/// SignalR hub for streaming real-time statistics to dashboard clients.
/// Clients call <see cref="JoinDashboard"/> on connect to start receiving snapshots.
/// </summary>
[Authorize]
public sealed class StatisticsHub(ISnapshotStore store) : Hub
{
    private const string DashboardGroup = "dashboard";

    /// <summary>
    /// Adds the caller to the "dashboard" group and immediately sends the current snapshot.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    public async Task JoinDashboard()
    {
        await this.Groups.AddToGroupAsync(this.Context.ConnectionId, DashboardGroup).ConfigureAwait(false);

        // Send current snapshot immediately so the client has data before the next push
        await this.Clients.Caller.SendAsync("ReceiveSnapshot", store.Current).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the caller from the "dashboard" group.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    public async Task LeaveDashboard()
    {
        await this.Groups.RemoveFromGroupAsync(this.Context.ConnectionId, DashboardGroup).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds the caller to a location-specific group and immediately sends the current location snapshot.
    /// </summary>
    /// <param name="locationNodeId">Location node identifier.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task JoinLocation(string locationNodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationNodeId);

        await this.Groups.AddToGroupAsync(this.Context.ConnectionId, BuildLocationGroup(locationNodeId)).ConfigureAwait(false);

        await this.Clients.Caller.SendAsync("ReceiveSnapshot", FilterSnapshot(store.Current, locationNodeId)).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the caller from a location-specific group.
    /// </summary>
    /// <param name="locationNodeId">Location node identifier.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task LeaveLocation(string locationNodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationNodeId);

        await this.Groups.RemoveFromGroupAsync(this.Context.ConnectionId, BuildLocationGroup(locationNodeId)).ConfigureAwait(false);
    }

    internal static string BuildLocationGroup(string locationNodeId) => $"location-{locationNodeId}";

    internal static StatSnapshot FilterSnapshot(StatSnapshot snapshot, string locationNodeId)
    {
        return snapshot with
        {
            DisplayJobs = snapshot.DisplayJobs
                .Where(job => string.Equals(job.LocationNodeId, locationNodeId, StringComparison.Ordinal))
                .ToList(),
            DisplayJobDetails = snapshot.DisplayJobDetails
                .Where(detail => string.Equals(detail.LocationNodeId, locationNodeId, StringComparison.Ordinal))
                .ToList(),
            AccessPoints = snapshot.AccessPoints
                .Where(accessPoint => string.Equals(accessPoint.LocationNodeId, locationNodeId, StringComparison.Ordinal))
                .ToList(),
        };
    }
}
