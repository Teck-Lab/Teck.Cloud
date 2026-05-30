using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Events;
using Statistic.Application.Statistics;
using Statistic.Domain.Statistics;
using Statistic.Infrastructure.Hubs;
using Statistic.Infrastructure.Statistics;

namespace Statistic.UnitTests.Infrastructure.Statistics;

public sealed class AccessPointRegisteredHandlerTests
{
    [Fact]
    public async Task Handle_ShouldAddNewMetricAndBroadcast()
    {
        // Arrange
        (AccessPointRegisteredHandler handler, ISnapshotStore store, IClientProxy locationProxy, IClientProxy dashboardProxy) = BuildHandler(new StatSnapshot([], [], [], [], [], [], []));

        // Act
        await handler.Handle(new AccessPointRegisteredIntegrationEvent(Guid.NewGuid(), "AP-10", "Hanshow", "loc-a", 8, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        // Assert
        store.Received().Update(Arg.Is<StatSnapshot>(s => s.AccessPoints.Any(ap => ap.SerialNumber == "AP-10" && ap.Vendor == "Hanshow" && ap.Status == "Registered" && ap.MaxCapacity == 8)));
        await locationProxy.Received().SendCoreAsync("ReceiveSnapshot", Arg.Any<object?[]>(), TestContext.Current.CancellationToken);
        await dashboardProxy.Received().SendCoreAsync("ReceiveSnapshot", Arg.Any<object?[]>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_ShouldOverwriteExistingMetricAndBroadcast()
    {
        // Arrange
        StatSnapshot current = new([], [], [], [], [], [], [new AccessPointMetric("AP-10", "Old", "loc-a", "Unknown", 5, 5)]);
        (AccessPointRegisteredHandler handler, ISnapshotStore store, IClientProxy locationProxy, IClientProxy dashboardProxy) = BuildHandler(current);

        // Act
        await handler.Handle(new AccessPointRegisteredIntegrationEvent(Guid.NewGuid(), "AP-10", "Hanshow", "loc-a", 12, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        // Assert
        store.Received().Update(Arg.Is<StatSnapshot>(s => s.AccessPoints.Any(ap => ap.SerialNumber == "AP-10" && ap.Vendor == "Hanshow" && ap.CurrentLoad == 0 && ap.MaxCapacity == 12 && ap.Status == "Registered")));
        await locationProxy.Received().SendCoreAsync("ReceiveSnapshot", Arg.Any<object?[]>(), TestContext.Current.CancellationToken);
        await dashboardProxy.Received().SendCoreAsync("ReceiveSnapshot", Arg.Any<object?[]>(), TestContext.Current.CancellationToken);
    }

    private static (AccessPointRegisteredHandler Handler, ISnapshotStore Store, IClientProxy LocationProxy, IClientProxy DashboardProxy) BuildHandler(StatSnapshot current)
    {
        ISnapshotStore store = Substitute.For<ISnapshotStore>();
        IHubContext<StatisticsHub> hubContext = Substitute.For<IHubContext<StatisticsHub>>();
        IHubClients clients = Substitute.For<IHubClients>();
        IClientProxy locationProxy = Substitute.For<IClientProxy>();
        IClientProxy dashboardProxy = Substitute.For<IClientProxy>();
        ILogger<AccessPointRegisteredHandler> logger = Substitute.For<ILogger<AccessPointRegisteredHandler>>();

        store.Current.Returns(current);
        hubContext.Clients.Returns(clients);
        clients.Group("location-loc-a").Returns(locationProxy);
        clients.Group("dashboard").Returns(dashboardProxy);

        return (new AccessPointRegisteredHandler(store, hubContext, logger), store, locationProxy, dashboardProxy);
    }
}
