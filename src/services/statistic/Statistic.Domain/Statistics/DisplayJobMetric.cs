// <copyright file="DisplayJobMetric.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Statistic.Domain.Statistics;

/// <summary>
/// Display operation queue metrics for a single location node.
/// </summary>
/// <param name="LocationNodeId">Location node identifier.</param>
/// <param name="ActiveJobs">Number of started display jobs that have not completed or failed.</param>
/// <param name="PendingJobs">Number of queued display jobs waiting to start.</param>
/// <param name="CompletedJobsToday">Number of display jobs completed today.</param>
/// <param name="FailedJobsToday">Number of display jobs failed today.</param>
/// <param name="LastUpdated">Timestamp of the most recent state change for this location.</param>
public sealed record DisplayJobMetric(
    string LocationNodeId,
    int ActiveJobs,
    int PendingJobs,
    int CompletedJobsToday,
    int FailedJobsToday,
    DateTimeOffset LastUpdated);
