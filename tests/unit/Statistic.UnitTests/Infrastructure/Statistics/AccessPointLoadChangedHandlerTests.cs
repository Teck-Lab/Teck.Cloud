using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Events;
using Statistic.Application.Statistics;
using Statistic.Domain.Statistics;
using Statistic.Infrastructure.Hubs;
using Statistic.Infrastructure.Statistics;

namespace Statistic.UnitTests.Infrastructure.Statistics;

public sealed class AccessPointLoadChangedHandlerTests
{
    [Fact]
    public async Task Handle_ShouldUpdateExistingAccessPointAndBroadcast()
    {
        // Arrange
        (AccessPointLoadChangedHandler handler, ISnapshotStore store, IClientProxy locationProxy, IClientProxy dashboardProxy) = BuildHandler(
            new StatSnapshot([], [], [], [], [], [], [new AccessPointMetric("AP-1", "Hanshow", "loc-1", "Registered", 0, 10)]));

        // Act
        await handler.Handle(new AccessPointLoadChangedIntegrationEvent(Guid.NewGuid(), "AP-1", "loc-1", 0, 3, 10, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        // Assert
        store.Received().Update(Arg.Is<StatSnapshot>(s => s.AccessPoints.Any(ap => ap.SerialNumber == "AP-1" && ap.CurrentLoad == 3 && ap.MaxCapacity == 10)));
        await locationProxy.Received().SendCoreAsync("ReceiveSnapshot", Arg.Any<object?[]>(), TestContext.Current.CancellationToken);
        await dashboardProxy.Received().SendCoreAsync("ReceiveSnapshot", Arg.Any<object?[]>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_ShouldCreateDefensivelyWhenAccessPointNotFoundAndBroadcast()
    {
        // Arrange
        (AccessPointLoadChangedHandler handler, ISnapshotStore store, IClientProxy locationProxy, IClientProxy dashboardProxy) = BuildHandler(new StatSnapshot([], [], [], [], [], [], []));

        // Act
        await handler.Handle(new AccessPointLoadChangedIntegrationEvent(Guid.NewGuid(), "AP-2", "loc-2", 0, 1, 5, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        // Assert
        store.Received().Update(Arg.Is<StatSnapshot>(s => s.AccessPoints.Any(ap => ap.SerialNumber == "AP-2" && ap.Status == "Unknown" && ap.CurrentLoad == 1 && ap.MaxCapacity == 5)));
        await locationProxy.Received().SendCoreAsync("ReceiveSnapshot", Arg.Any<object?[]>(), TestContext.Current.CancellationToken);
        await dashboardProxy.Received().SendCoreAsync("ReceiveSnapshot", Arg.Any<object?[]>(), TestContext.Current.CancellationToken);
    }

    private static (AccessPointLoadChangedHandler Handler, ISnapshotStore Store, IClientProxy LocationProxy, IClientProxy DashboardProxy) BuildHandler(StatSnapshot current)
    {
        ISnapshotStore store = Substitute.For<ISnapshotStore>();
        IHubContext<StatisticsHub> hubContext = Substitute.For<IHubContext<StatisticsHub>>();
        IHubClients clients = Substitute.For<IHubClients>();
        IClientProxy locationProxy = Substitute.For<IClientProxy>();
        IClientProxy dashboardProxy = Substitute.For<IClientProxy>();
        ILogger<AccessPointLoadChangedHandler> logger = Substitute.For<ILogger<AccessPointLoadChangedHandler>>();

        store.Current.Returns(current);
        hubContext.Clients.Returns(clients);
        clients.Group("location-loc-1").Returns(locationProxy);
        clients.Group("location-loc-2").Returns(locationProxy);
        clients.Group("dashboard").Returns(dashboardProxy);

        return (new AccessPointLoadChangedHandler(store, hubContext, logger), store, locationProxy, dashboardProxy);
    }
}
