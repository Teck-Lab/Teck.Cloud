using Shouldly;
using Xunit;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Fixed customer page membership, refreshed projections, sort ties and opaque cursor behavior.</summary>
public sealed partial class CustomerGatewayMatrixTests
{
    /// <summary>A nonactive reread replaces only its item, reorders the fixed page and preserves the original cursor.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task PagedRereadReordersFixedPageWithoutRefillOrCursorChange()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var script = new FakeCommerceScript()
            .Page(ListOrdersReply.Ok(
                [ActiveA(CommerceFixtures.ListOrderRevision), ActiveB(CommerceFixtures.ListOrderRevision)],
                CommerceFixtures.Cursor))
            .License(OrderAKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Ok(CommerceFixtures.Envelope()))
            .License(OrderBKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Error(Grpc.Core.StatusCode.NotFound, "not_found"))
            .Order(OrderBKey, CustomerOrderReply.Ok(CommerceFixtures.PendingReadinessOrder(
                CommerceFixtures.OrderB,
                CommerceFixtures.SubscriptionB,
                CommerceFixtures.BRereadAt,
                CommerceFixtures.PublicOrderRevision)));
        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, GatewaySurface.List, token, cancellation).ConfigureAwait(true);

        response.ShouldBeListPage([OrderBKey, OrderAKey], CommerceFixtures.Cursor);
        response.Order(GatewaySurface.List, CommerceFixtures.OrderB).ShouldBePendingReadiness(
            CommerceFixtures.PublicOrderRevision,
            CommerceFixtures.BRereadAt);
        response.Order(GatewaySurface.List, CommerceFixtures.OrderA).ShouldBeActiveOrder(
            CommerceFixtures.OrderA,
            CommerceFixtures.SubscriptionA,
            CommerceFixtures.ListOrderRevision);
        AssertFixedPageCalls(harness, CommerceFixtures.OrderB);
    }

    /// <summary>The final page keeps an explicit null cursor even after a reread replacement.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task PagedRereadPreservesNullCursor()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var script = new FakeCommerceScript()
            .Page(ListOrdersReply.OkWithoutCursor(
                [ActiveA(CommerceFixtures.ListOrderRevision), ActiveB(CommerceFixtures.ListOrderRevision)]))
            .License(OrderAKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Ok(CommerceFixtures.Envelope()))
            .License(OrderBKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Error(Grpc.Core.StatusCode.NotFound, "not_found"))
            .Order(OrderBKey, CustomerOrderReply.Ok(CommerceFixtures.PendingReadinessOrder(
                CommerceFixtures.OrderB,
                CommerceFixtures.SubscriptionB,
                CommerceFixtures.BRereadAt,
                CommerceFixtures.PublicOrderRevision)));
        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, GatewaySurface.List, token, cancellation).ConfigureAwait(true);

        response.ShouldBeListPage([OrderBKey, OrderAKey], nextCursor: null);
        response.Order(GatewaySurface.List, CommerceFixtures.OrderB).ShouldHaveNoLicense();
        AssertFixedPageCalls(harness, CommerceFixtures.OrderB);
    }

    /// <summary>A refreshed projection with an equal updated timestamp sorts by order ID ascending.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task EqualUpdatedTimestampUsesOrderIdTieBreak()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var script = new FakeCommerceScript()
            .Page(ListOrdersReply.Ok(
                [
                    ActiveB(CommerceFixtures.ListOrderRevision, updatedAt: CommerceFixtures.AUpdatedAt),
                    ActiveA(CommerceFixtures.ListOrderRevision, updatedAt: CommerceFixtures.BUpdatedAt),
                ],
                CommerceFixtures.Cursor))
            .License(OrderBKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Ok(CommerceFixtures.Envelope()))
            .License(OrderAKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Error(Grpc.Core.StatusCode.NotFound, "not_found"))
            .Order(OrderAKey, CustomerOrderReply.Ok(PendingReadinessA(CommerceFixtures.AUpdatedAt, CommerceFixtures.PublicOrderRevision)));
        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, GatewaySurface.List, token, cancellation).ConfigureAwait(true);

        response.ShouldBeListPage([OrderAKey, OrderBKey], CommerceFixtures.Cursor);
        response.Order(GatewaySurface.List, CommerceFixtures.OrderA).ShouldBePendingReadiness(
            CommerceFixtures.PublicOrderRevision,
            CommerceFixtures.AUpdatedAt);
        response.Order(GatewaySurface.List, CommerceFixtures.OrderB).ShouldBeActiveOrder(
            CommerceFixtures.OrderB,
            CommerceFixtures.SubscriptionB,
            CommerceFixtures.ListOrderRevision);
        AssertFixedPageCalls(harness, CommerceFixtures.OrderA);
    }

    private void AssertFixedPageCalls(GatewayHarness harness, Guid rereadOrder)
    {
        var listCalls = harness.Recorder.For("ListCustomerOrders");
        listCalls.Count.ShouldBe(1);
        listCalls[0].Cursor.ShouldBe(CommerceFixtures.Cursor);
        listCalls[0].Limit.ShouldBe(100u);
        listCalls[0].MetadataValue(TenantMetadataKey).ShouldBe(_keycloak.CustomerA.TenantId.ToString("D"));
        harness.Recorder.Count("GetCustomerOrder", rereadOrder).ShouldBe(1);
        var licensing = LicensingCallsFor(harness, rereadOrder);
        licensing.Length.ShouldBe(1);
        licensing[0].DesiredRevision.ShouldBe(CommerceFixtures.InitialDesiredRevision);
        licensing.Any(call => call.DesiredRevision == CommerceFixtures.RereadDesiredRevision).ShouldBeFalse();
        harness.Recorder.For("GetCurrentLicense").Count.ShouldBe(2);
    }
}
