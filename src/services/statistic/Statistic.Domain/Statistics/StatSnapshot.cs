// <copyright file="StatSnapshot.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Statistic.Domain.Statistics;

/// <summary>
/// Immutable snapshot of all statistics pushed to connected dashboard clients.
/// </summary>
/// <param name="MonthlyData">Monthly orders and revenue for the trailing 12 months.</param>
/// <param name="TenantData">Per-tenant order counts.</param>
/// <param name="PieData">Order status breakdown.</param>
/// <param name="RecentActivity">Latest activity feed events.</param>
/// <param name="DisplayJobs">Display operation queue metrics grouped by location.</param>
/// <param name="DisplayJobDetails">Display operation details grouped by display and location.</param>
/// <param name="AccessPoints">Access point metrics grouped across locations.</param>
public sealed record StatSnapshot(
    IReadOnlyList<MonthlyMetric> MonthlyData,
    IReadOnlyList<TenantMetric> TenantData,
    IReadOnlyList<PieMetric> PieData,
    IReadOnlyList<ActivityEvent> RecentActivity,
    IReadOnlyList<DisplayJobMetric> DisplayJobs,
    IReadOnlyList<DisplayJobDetail> DisplayJobDetails,
    IReadOnlyList<AccessPointMetric> AccessPoints);
