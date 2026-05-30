// <copyright file="ActivityEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Statistic.Domain.Statistics;

/// <summary>
/// Represents a single entry in the activity feed.
/// </summary>
/// <param name="Time">Human-readable relative time (e.g. "2 min ago").</param>
/// <param name="Text">Description of the event.</param>
public sealed record ActivityEvent(string Time, string Text);
