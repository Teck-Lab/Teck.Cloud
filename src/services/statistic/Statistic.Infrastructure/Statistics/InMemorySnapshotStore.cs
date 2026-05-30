// <copyright file="InMemorySnapshotStore.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Statistic.Application.Statistics;
using Statistic.Domain.Statistics;

namespace Statistic.Infrastructure.Statistics;

/// <summary>
/// In-memory implementation of <see cref="ISnapshotStore"/>.
/// Seeded with realistic baseline data on first access.
/// </summary>
internal sealed class InMemorySnapshotStore : ISnapshotStore
{
    private static readonly string[] Months =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    private static readonly string[] Tenants =
        ["Acme Corp", "Globex", "Initech", "Umbrella", "Hooli"];

    private static readonly string[] PieNames =
        ["Completed", "Pending", "Cancelled", "Refunded"];

    private volatile StatSnapshot current = BuildSeed();

    /// <inheritdoc/>
    public StatSnapshot Current => this.current;

    /// <inheritdoc/>
    public void Update(StatSnapshot snapshot) => this.current = snapshot;

    private static StatSnapshot BuildSeed()
    {
        var monthly = Months.Select((month, index) => new MonthlyMetric(
            month,
            Orders: 300 + (index * 40),
            Revenue: 22000m + (index * 3500m))).ToList();

        var tenants = Tenants.Select((tenantName, index) => new TenantMetric(tenantName, Orders: 140 - (index * 25))).ToList();

        var pie = PieNames.Select((pieName, index) => new PieMetric(pieName, Value: 2400 - (index * 530))).ToList();

        var activity = new List<ActivityEvent>
        {
            new("2 min ago", "Order #4821 placed by Acme Corp"),
            new("18 min ago", "Tenant Globex Inc onboarded"),
            new("1 hr ago", "Label batch exported — 200 items"),
            new("3 hr ago", "Payment confirmed for order #4800"),
        };

        return new StatSnapshot(monthly, tenants, pie, activity, [], [], []);
    }
}
