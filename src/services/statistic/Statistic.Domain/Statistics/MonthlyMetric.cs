// <copyright file="MonthlyMetric.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Statistic.Domain.Statistics;

/// <summary>
/// Represents order and revenue totals for a single month.
/// </summary>
/// <param name="Month">Abbreviated month name (e.g. "Jan").</param>
/// <param name="Orders">Total order count.</param>
/// <param name="Revenue">Total revenue.</param>
public sealed record MonthlyMetric(string Month, int Orders, decimal Revenue);
