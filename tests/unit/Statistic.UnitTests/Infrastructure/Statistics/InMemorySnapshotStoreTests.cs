using Shouldly;
using Statistic.Application.Statistics;
using Statistic.Domain.Statistics;

namespace Statistic.UnitTests.Infrastructure.Statistics;

public sealed class InMemorySnapshotStoreTests
{
    [Fact]
    public void Current_WhenCreated_ShouldExposeSeedSnapshot()
    {
        // Arrange
        ISnapshotStore sut = CreateStore();

        // Act
        StatSnapshot snapshot = sut.Current;

        // Assert
        snapshot.MonthlyData.Count.ShouldBeGreaterThan(0);
        snapshot.TenantData.Count.ShouldBeGreaterThan(0);
        snapshot.PieData.Count.ShouldBeGreaterThan(0);
        snapshot.RecentActivity.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Update_WhenCalled_ShouldReplaceCurrentSnapshot()
    {
        // Arrange
        ISnapshotStore sut = CreateStore();
        StatSnapshot updated = new(
            [new MonthlyMetric("Feb", 20, 400m)],
            [new TenantMetric("Acme", 7)],
            [new PieMetric("Completed", 12)],
            [new ActivityEvent("now", "updated")],
            [],
            [],
            [new AccessPointMetric("AP-9", "Hanshow", "loc-b", "Online", 3, 10)]);

        // Act
        sut.Update(updated);

        // Assert
        sut.Current.ShouldBeSameAs(updated);
        sut.Current.AccessPoints.Single().SerialNumber.ShouldBe("AP-9");
    }

    [Fact]
    public async Task Update_WhenConcurrentWriters_ShouldKeepValidSnapshotReference()
    {
        // Arrange
        ISnapshotStore sut = CreateStore();
        StatSnapshot[] snapshots =
        [
            new([], [], [], [], [], [], [new AccessPointMetric("AP-1", "Vendor", "loc-1", "Online", 1, 1)]),
            new([], [], [], [], [], [], [new AccessPointMetric("AP-2", "Vendor", "loc-2", "Offline", 2, 2)]),
            new([], [], [], [], [], [], [new AccessPointMetric("AP-3", "Vendor", "loc-3", "Registered", 3, 3)]),
            new([], [], [], [], [], [], [new AccessPointMetric("AP-4", "Vendor", "loc-4", "Unknown", 4, 4)]),
        ];

        // Act
        await Parallel.ForEachAsync(snapshots, TestContext.Current.CancellationToken, (snapshot, _) =>
        {
            sut.Update(snapshot);
            return ValueTask.CompletedTask;
        });

        StatSnapshot currentAfterParallel = sut.Current;

        StatSnapshot finalSnapshot = new([], [], [], [], [], [], [new AccessPointMetric("AP-final", "Vendor", "loc-z", "Online", 99, 100)]);
        sut.Update(finalSnapshot);

        // Assert
        snapshots.Contains(currentAfterParallel).ShouldBeTrue();
        sut.Current.ShouldBeSameAs(finalSnapshot);
        sut.Current.AccessPoints.Single().SerialNumber.ShouldBe("AP-final");
    }

    private static ISnapshotStore CreateStore()
    {
        Type storeType = Type.GetType("Statistic.Infrastructure.Statistics.InMemorySnapshotStore, Statistic.Infrastructure")!;
        return (ISnapshotStore)Activator.CreateInstance(storeType)!;
    }
}
