using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Events;
using Statistic.Application.Statistics;
using Statistic.Domain.Statistics;
using Statistic.Infrastructure.Hubs;
using Statistic.Infrastructure.Statistics;

namespace Statistic.UnitTests.Infrastructure.Statistics;

public sealed class AccessPointStatusChangedHandlerTests
{
    [Fact]
    public async Task Handle_ShouldUpdateExistingStatusAndBroadcast()
    {
        // Arrange
        StatSnapshot current = new([], [], [], [], [], [], [new AccessPointMetric("AP-100", "Hanshow", "loc-k", "Registered", 1, 10)]);
        (AccessPointStatusChangedHandler handler, ISnapshotStore store, IClientProxy locationProxy, IClientProxy dashboardProxy) = BuildHandler(current);

        // Act
        await handler.Handle(new AccessPointStatusChangedIntegrationEvent(Guid.NewGuid(), "AP-100", "Registered", "Offline", "loc-k", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        // Assert
        store.Received().Update(Arg.Is<StatSnapshot>(s => s.AccessPoints.Any(ap => ap.SerialNumber == "AP-100" && ap.Status == "Offline")));
        await locationProxy.Received().SendCoreAsync("ReceiveSnapshot", Arg.Any<object?[]>(), TestContext.Current.CancellationToken);
        await dashboardProxy.Received().SendCoreAsync("ReceiveSnapshot", Arg.Any<object?[]>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_ShouldCreateDefensivelyWhenMissingAndBroadcast()
    {
        // Arrange
        (AccessPointStatusChangedHandler handler, ISnapshotStore store, IClientProxy locationProxy, IClientProxy dashboardProxy) = BuildHandler(new StatSnapshot([], [], [], [], [], [], []));

        // Act
        await handler.Handle(new AccessPointStatusChangedIntegrationEvent(Guid.NewGuid(), "AP-101", "Unknown", "Online", "loc-m", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        // Assert
        store.Received().Update(Arg.Is<StatSnapshot>(s => s.AccessPoints.Any(ap => ap.SerialNumber == "AP-101" && ap.Status == "Online" && ap.Vendor == string.Empty && ap.MaxCapacity == 0)));
        await locationProxy.Received().SendCoreAsync("ReceiveSnapshot", Arg.Any<object?[]>(), TestContext.Current.CancellationToken);
        await dashboardProxy.Received().SendCoreAsync("ReceiveSnapshot", Arg.Any<object?[]>(), TestContext.Current.CancellationToken);
    }

    private static (AccessPointStatusChangedHandler Handler, ISnapshotStore Store, IClientProxy LocationProxy, IClientProxy DashboardProxy) BuildHandler(StatSnapshot current)
    {
        ISnapshotStore store = Substitute.For<ISnapshotStore>();
        IHubContext<StatisticsHub> hubContext = Substitute.For<IHubContext<StatisticsHub>>();
        IHubClients clients = Substitute.For<IHubClients>();
        IClientProxy locationProxy = Substitute.For<IClientProxy>();
        IClientProxy dashboardProxy = Substitute.For<IClientProxy>();
        ILogger<AccessPointStatusChangedHandler> logger = Substitute.For<ILogger<AccessPointStatusChangedHandler>>();

        store.Current.Returns(current);
        hubContext.Clients.Returns(clients);
        clients.Group("location-loc-k").Returns(locationProxy);
        clients.Group("location-loc-m").Returns(locationProxy);
        clients.Group("dashboard").Returns(dashboardProxy);

        return (new AccessPointStatusChangedHandler(store, hubContext, logger), store, locationProxy, dashboardProxy);
    }
}
