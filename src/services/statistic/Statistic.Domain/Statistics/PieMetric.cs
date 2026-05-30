// <copyright file="PieMetric.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Statistic.Domain.Statistics;

/// <summary>
/// Represents a single slice of a pie/donut chart (e.g. order status breakdown).
/// </summary>
/// <param name="Name">Slice label.</param>
/// <param name="Value">Numeric value for this slice.</param>
public sealed record PieMetric(string Name, int Value);
