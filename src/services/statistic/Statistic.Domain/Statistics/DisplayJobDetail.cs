// <copyright file="DisplayJobDetail.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Statistic.Domain.Statistics;

/// <summary>
/// Display operation details for a single display job.
/// </summary>
/// <param name="LocationNodeId">Location node identifier.</param>
/// <param name="DisplayId">Display identifier.</param>
/// <param name="OperationType">Display operation type.</param>
/// <param name="Status">Current operation status.</param>
/// <param name="RequestedAt">Timestamp when the operation was requested.</param>
/// <param name="StartedAt">Timestamp when the operation started.</param>
/// <param name="CompletedAt">Timestamp when the operation completed or failed.</param>
/// <param name="FailureReason">Failure reason when the operation failed.</param>
public sealed record DisplayJobDetail(
    string LocationNodeId,
    Guid DisplayId,
    string OperationType,
    string Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason);
