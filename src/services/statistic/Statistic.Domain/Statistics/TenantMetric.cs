// <copyright file="TenantMetric.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Statistic.Domain.Statistics;

/// <summary>
/// Represents order totals for a single tenant.
/// </summary>
/// <param name="Name">Tenant display name.</param>
/// <param name="Orders">Total order count for this tenant.</param>
public sealed record TenantMetric(string Name, int Orders);
