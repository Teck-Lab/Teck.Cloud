using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Events;
using Statistic.Application.Statistics;
using Statistic.Domain.Statistics;
using Statistic.Infrastructure.Hubs;
using Statistic.Infrastructure.Statistics;

namespace Statistic.UnitTests.Infrastructure.Statistics;

public sealed class DisplayOperationStateChangedHandlerTests
{
    [Fact]
    public async Task Handle_ShouldUpdateMetricsDetailsBroadcastAndTrimTerminalDetails()
    {
        // Arrange
        ISnapshotStore store = Substitute.For<ISnapshotStore>();
        IHubContext<StatisticsHub> hubContext = Substitute.For<IHubContext<StatisticsHub>>();
        IHubClients clients = Substitute.For<IHubClients>();
        IClientProxy locationProxy = Substitute.For<IClientProxy>();
        IClientProxy dashboardProxy = Substitute.For<IClientProxy>();
        ILogger<DisplayOperationStateChangedHandler> logger = Substitute.For<ILogger<DisplayOperationStateChangedHandler>>();

        hubContext.Clients.Returns(clients);
        clients.Group("location-loc-1").Returns(locationProxy);
        clients.Group("dashboard").Returns(dashboardProxy);

        Guid displayId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<DisplayJobDetail> details = Enumerable.Range(0, 60)
            .Select(i => new DisplayJobDetail("loc-1", Guid.NewGuid(), "Assign", "Completed", now.AddMinutes(-i), null, now.AddMinutes(-i), null))
            .ToList();

        StatSnapshot current = new([], [], [], [], [], details, []);
        store.Current.Returns(current);

        DisplayOperationStateChangedHandler handler = new(store, hubContext, logger);

        // Act
        await handler.Handle(new DisplayOperationStateChangedIntegrationEvent(displayId, "loc-1", "tenant-a", "Assign", "Queued", 1, now), TestContext.Current.CancellationToken);
        await handler.Handle(new DisplayOperationStateChangedIntegrationEvent(displayId, "loc-1", "tenant-a", "Assign", "Started", 0, now.AddSeconds(1)), TestContext.Current.CancellationToken);
        await handler.Handle(new DisplayOperationStateChangedIntegrationEvent(displayId, "loc-1", "tenant-a", "Assign", "Completed", 0, now.AddSeconds(2)), TestContext.Current.CancellationToken);
        await handler.Handle(new DisplayOperationStateChangedIntegrationEvent(displayId, "loc-1", "tenant-a", "Assign", "Failed", 0, now.AddSeconds(3)), TestContext.Current.CancellationToken);

        // Assert
        store.Received().Update(Arg.Is<StatSnapshot>(snapshot =>
            snapshot.DisplayJobs.Any(metric => metric.LocationNodeId == "loc-1") &&
            snapshot.DisplayJobDetails.Any(detail => detail.DisplayId == displayId && detail.LocationNodeId == "loc-1") &&
            snapshot.DisplayJobDetails.Count(detail => detail.LocationNodeId == "loc-1" && (detail.Status == "Completed" || detail.Status == "Failed")) <= 50));

        await locationProxy.Received().SendCoreAsync("ReceiveSnapshot", Arg.Any<object?[]>(), TestContext.Current.CancellationToken);
        await locationProxy.Received().SendCoreAsync("ReceiveNotification", Arg.Any<object?[]>(), TestContext.Current.CancellationToken);
        await dashboardProxy.Received().SendCoreAsync("ReceiveSnapshot", Arg.Any<object?[]>(), TestContext.Current.CancellationToken);
        await dashboardProxy.Received().SendCoreAsync("ReceiveNotification", Arg.Any<object?[]>(), TestContext.Current.CancellationToken);
    }
}
