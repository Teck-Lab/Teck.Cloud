using Shouldly;
using Statistic.Application.Statistics;
using Statistic.Application.Statistics.Features.GetCurrentSnapshot.V1;
using Statistic.Domain.Statistics;

namespace Statistic.UnitTests.Application.Statistics.Features.GetCurrentSnapshot.V1;

public sealed class GetCurrentSnapshotQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenSnapshotExists_ShouldReturnCurrentSnapshot()
    {
        // Arrange
        StatSnapshot expectedSnapshot = new(
            [new MonthlyMetric("Jan", 10, 100m)],
            [],
            [],
            [],
            [],
            [],
            [new AccessPointMetric("AP-1", "Hanshow", "loc-a", "Online", 2, 8)]);

        ISnapshotStore store = new TestSnapshotStore(expectedSnapshot);
        object sut = CreateHandler(store);

        // Act
        StatSnapshot result = await InvokeHandleAsync(sut, new GetCurrentSnapshotQuery());

        // Assert
        result.ShouldBeSameAs(expectedSnapshot);
        result.AccessPoints.Count.ShouldBe(1);
        result.AccessPoints[0].SerialNumber.ShouldBe("AP-1");
    }

    [Fact]
    public async Task Handle_WhenSnapshotIsEmpty_ShouldReturnEmptySnapshot()
    {
        // Arrange
        StatSnapshot emptySnapshot = new([], [], [], [], [], [], []);
        ISnapshotStore store = new TestSnapshotStore(emptySnapshot);
        object sut = CreateHandler(store);

        // Act
        StatSnapshot result = await InvokeHandleAsync(sut, new GetCurrentSnapshotQuery());

        // Assert
        result.ShouldBeSameAs(emptySnapshot);
        result.MonthlyData.ShouldBeEmpty();
        result.TenantData.ShouldBeEmpty();
        result.PieData.ShouldBeEmpty();
        result.RecentActivity.ShouldBeEmpty();
        result.DisplayJobs.ShouldBeEmpty();
        result.DisplayJobDetails.ShouldBeEmpty();
        result.AccessPoints.ShouldBeEmpty();
    }

    private sealed class TestSnapshotStore(StatSnapshot snapshot) : ISnapshotStore
    {
        public StatSnapshot Current => snapshot;

        public void Update(StatSnapshot snapshot)
        {
        }
    }

    private static object CreateHandler(ISnapshotStore store)
    {
        Type handlerType = Type.GetType("Statistic.Application.Statistics.Features.GetCurrentSnapshot.V1.GetCurrentSnapshotQueryHandler, Statistic.Application")!;
        return Activator.CreateInstance(handlerType, store)!;
    }

    private static async ValueTask<StatSnapshot> InvokeHandleAsync(object handler, GetCurrentSnapshotQuery query)
    {
        var handleMethod = handler.GetType().GetMethod("Handle")!;
        var valueTask = (ValueTask<StatSnapshot>)handleMethod.Invoke(handler, [query, TestContext.Current.CancellationToken])!;
        return await valueTask;
    }
}
