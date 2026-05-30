// <copyright file="DashboardNotification.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Statistic.Domain.Statistics;

/// <summary>
/// A push notification delivered to connected dashboard clients via SignalR.
/// </summary>
/// <param name="Id">Unique identifier for this notification.</param>
/// <param name="Title">Short headline shown in the toast and notification drawer.</param>
/// <param name="Message">Longer description text.</param>
/// <param name="Level">Severity: <c>info</c>, <c>success</c>, <c>warning</c>, or <c>error</c>.</param>
/// <param name="OccurredAt">ISO-8601 UTC timestamp of when the event occurred.</param>
public sealed record DashboardNotification(
    string Id,
    string Title,
    string Message,
    string Level,
    string OccurredAt);
