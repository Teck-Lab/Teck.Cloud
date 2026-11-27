using Commerce.IntegrationTests.Infrastructure;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Teck.Cloud.Commerce.Orders.V1;
using Xunit;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Real HTTP customer gateway regression over controllable HTTP/2 Orders and Licensing replies.</summary>
/// <param name="keycloak">Shared isolated Keycloak fixture for authenticated customer principals.</param>
[Collection(CommerceIntegrationSuite.Name)]
public sealed partial class CustomerGatewayMatrixTests(CommerceKeycloakFixture keycloak)
{
    private const string TenantMetadataKey = "teck-tenant-id";

    private readonly CommerceKeycloakFixture _keycloak = keycloak;

    private static string OrderAKey => CommerceFixtures.OrderA.ToString("D");

    private static string OrderBKey => CommerceFixtures.OrderB.ToString("D");

    /// <summary>The first active lookup uses generation 7; a still-active reread confirms generation 8 exactly once.</summary>
    /// <param name="surface">Customer HTTP surface under test.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData(GatewaySurface.Detail)]
    [InlineData(GatewaySurface.List)]
    [InlineData(GatewaySurface.Replay)]
    public async Task ActiveSnapshotReadsGenerationSevenThenConfirmsGenerationEight(GatewaySurface surface)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var script = ActiveScript(surface)
            .License(OrderAKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Error(StatusCode.NotFound, "not_found"))
            .Order(OrderAKey, CustomerOrderReply.Ok(ActiveA(CommerceFixtures.PublicOrderRevision, CommerceFixtures.RereadDesiredRevision)))
            .License(OrderAKey, CommerceFixtures.RereadDesiredRevision, LicenseReply.Ok(CommerceFixtures.Envelope()));
        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, surface, token, cancellation).ConfigureAwait(true);

        var order = response.Order(surface, CommerceFixtures.OrderA);
        response.ShouldBeActiveSuccess(surface, order, CommerceFixtures.OrderA, CommerceFixtures.SubscriptionA, CommerceFixtures.PublicOrderRevision);
        if (surface == GatewaySurface.List)
        {
            response.ShouldBeListPage([OrderAKey, OrderBKey], CommerceFixtures.Cursor);
            response.Order(surface, CommerceFixtures.OrderB).ShouldBeActiveOrder(CommerceFixtures.OrderB, CommerceFixtures.SubscriptionB, CommerceFixtures.ListOrderRevision);
            var sibling = LicensingCallsFor(harness, CommerceFixtures.OrderB);
            sibling.Length.ShouldBe(1);
            ShouldTargetScope(sibling[0], CommerceFixtures.OrderB, CommerceFixtures.SubscriptionB, CommerceFixtures.InitialDesiredRevision);
            harness.Recorder.For("ListCustomerOrders").Single().MetadataValue(TenantMetadataKey).ShouldBe(_keycloak.CustomerA.TenantId.ToString("D"));
        }

        var licensing = LicensingCallsFor(harness, CommerceFixtures.OrderA);
        licensing.Length.ShouldBe(2);
        ShouldTargetScope(licensing[0], CommerceFixtures.OrderA, CommerceFixtures.SubscriptionA, CommerceFixtures.InitialDesiredRevision);
        ShouldTargetScope(licensing[1], CommerceFixtures.OrderA, CommerceFixtures.SubscriptionA, CommerceFixtures.RereadDesiredRevision);
        licensing[0].DesiredRevision.ShouldNotBe(CommerceFixtures.PublicOrderRevision);
        licensing[0].DesiredRevision.ShouldNotBe(CommerceFixtures.LicenseRevision);
        licensing[1].DesiredRevision.ShouldNotBe(CommerceFixtures.PublicOrderRevision);
        licensing[1].DesiredRevision.ShouldNotBe(CommerceFixtures.LicenseRevision);
        harness.Recorder.Count("GetCustomerOrder", CommerceFixtures.OrderA).ShouldBe(surface == GatewaySurface.Detail ? 2 : 1);
        harness.Recorder.For("ListCustomerOrders").Count.ShouldBe(surface == GatewaySurface.List ? 1 : 0);
        harness.Recorder.For("CreateOrder").Count.ShouldBe(surface == GatewaySurface.Replay ? 1 : 0);
        foreach (var orderCall in harness.Recorder.For("GetCustomerOrder").Where(call => string.Equals(call.OrderId, OrderAKey, StringComparison.Ordinal)))
        {
            orderCall.MetadataValue(TenantMetadataKey).ShouldBe(_keycloak.CustomerA.TenantId.ToString("D"));
        }

        harness.Recorder.For("GetCurrentLicense").Where(call =>
            string.Equals(call.OrderId, OrderAKey, StringComparison.Ordinal) && call.DesiredRevision != CommerceFixtures.InitialDesiredRevision && call.DesiredRevision != CommerceFixtures.RereadDesiredRevision).ShouldBeEmpty();
    }

    /// <summary>A clean initial license success returns the active order without any reread.</summary>
    /// <param name="surface">Customer HTTP surface under test.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData(GatewaySurface.Detail)]
    [InlineData(GatewaySurface.List)]
    [InlineData(GatewaySurface.Replay)]
    public async Task CleanInitialLicenseReturnsActiveProjectionWithoutReread(GatewaySurface surface)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var script = ActiveScript(surface)
            .License(OrderAKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Ok(CommerceFixtures.Envelope()));
        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, surface, token, cancellation).ConfigureAwait(true);

        var order = response.Order(surface, CommerceFixtures.OrderA);
        var expectedRevision = surface == GatewaySurface.List ? CommerceFixtures.ListOrderRevision : CommerceFixtures.PublicOrderRevision;
        response.ShouldBeActiveSuccess(surface, order, CommerceFixtures.OrderA, CommerceFixtures.SubscriptionA, expectedRevision);
        if (surface == GatewaySurface.List)
        {
            response.ShouldBeListPage([OrderAKey, OrderBKey], CommerceFixtures.Cursor);
        }

        var licensing = LicensingCallsFor(harness, CommerceFixtures.OrderA);
        licensing.Length.ShouldBe(1);
        ShouldTargetScope(licensing[0], CommerceFixtures.OrderA, CommerceFixtures.SubscriptionA, CommerceFixtures.InitialDesiredRevision);
        harness.Recorder.Count("GetCustomerOrder", CommerceFixtures.OrderA).ShouldBe(surface == GatewaySurface.Detail ? 1 : 0);
    }

    /// <summary>Every unsuccessful confirmation result fails the whole response ahead of ordinary mappings.</summary>
    /// <param name="surface">Customer HTTP surface under test.</param>
    /// <param name="failure">Confirmation failure mode.</param>
    /// <returns>The verification task.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="failure"/> is not a defined confirmation failure.</exception>
    [Theory]
    [InlineData(GatewaySurface.Detail, ConfirmationFailure.Forbidden)]
    [InlineData(GatewaySurface.Detail, ConfirmationFailure.NotFound)]
    [InlineData(GatewaySurface.Detail, ConfirmationFailure.Transport)]
    [InlineData(GatewaySurface.List, ConfirmationFailure.Forbidden)]
    [InlineData(GatewaySurface.List, ConfirmationFailure.NotFound)]
    [InlineData(GatewaySurface.List, ConfirmationFailure.Transport)]
    [InlineData(GatewaySurface.Replay, ConfirmationFailure.Forbidden)]
    [InlineData(GatewaySurface.Replay, ConfirmationFailure.NotFound)]
    [InlineData(GatewaySurface.Replay, ConfirmationFailure.Transport)]
    public async Task ConfirmationFailureFailsWholeResponseWithoutRereadOrPartialBody(GatewaySurface surface, ConfirmationFailure failure)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var confirmationReply = failure switch
        {
            ConfirmationFailure.Forbidden => LicenseReply.Error(StatusCode.PermissionDenied, "forbidden"),
            ConfirmationFailure.NotFound => LicenseReply.Error(StatusCode.NotFound, "not_found"),
            ConfirmationFailure.Transport => LicenseReply.TransportFailure(),
            _ => throw new ArgumentOutOfRangeException(nameof(failure)),
        };
        var script = ActiveScript(surface)
            .License(OrderAKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Error(StatusCode.NotFound, "not_found"))
            .Order(OrderAKey, CustomerOrderReply.Ok(ActiveA(CommerceFixtures.PublicOrderRevision, CommerceFixtures.RereadDesiredRevision)))
            .License(OrderAKey, CommerceFixtures.RereadDesiredRevision, confirmationReply);
        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, surface, token, cancellation).ConfigureAwait(true);

        response.ShouldBeProblem(StatusCodes.Status503ServiceUnavailable, "dependency_unavailable");
        response.ShouldNotContainOrderProjection(CommerceFixtures.OrderA, CommerceFixtures.OrderB);
        var licensing = LicensingCallsFor(harness, CommerceFixtures.OrderA);
        licensing.Length.ShouldBe(2);
        licensing[1].DesiredRevision.ShouldBe(CommerceFixtures.RereadDesiredRevision);
        harness.Recorder.Count("GetCustomerOrder", CommerceFixtures.OrderA).ShouldBe(surface == GatewaySurface.Detail ? 2 : 1);
        harness.Recorder.For("CreateOrder").Count.ShouldBe(surface == GatewaySurface.Replay ? 1 : 0);
    }

    /// <summary>Malformed Licensing successes, including an empty envelope and OK with an error trailer, fail closed.</summary>
    /// <param name="surface">Customer HTTP surface under test.</param>
    /// <param name="success">Malformed success variant.</param>
    /// <returns>The verification task.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="success"/> is not a defined malformed success variant.</exception>
    [Theory]
    [InlineData(GatewaySurface.Detail, MalformedLicenseSuccess.EmptyEnvelope)]
    [InlineData(GatewaySurface.Detail, MalformedLicenseSuccess.TraileredOk)]
    [InlineData(GatewaySurface.List, MalformedLicenseSuccess.EmptyEnvelope)]
    [InlineData(GatewaySurface.List, MalformedLicenseSuccess.TraileredOk)]
    [InlineData(GatewaySurface.Replay, MalformedLicenseSuccess.EmptyEnvelope)]
    [InlineData(GatewaySurface.Replay, MalformedLicenseSuccess.TraileredOk)]
    public async Task MalformedLicenseSuccessFailsClosedWithoutReread(GatewaySurface surface, MalformedLicenseSuccess success)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var malformedReply = success switch
        {
            MalformedLicenseSuccess.EmptyEnvelope => LicenseReply.Empty(),
            MalformedLicenseSuccess.TraileredOk => LicenseReply.Ok(CommerceFixtures.Envelope(), "invalid_request"),
            _ => throw new ArgumentOutOfRangeException(nameof(success)),
        };
        var script = ActiveScript(surface)
            .License(OrderAKey, CommerceFixtures.InitialDesiredRevision, malformedReply);
        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, surface, token, cancellation).ConfigureAwait(true);

        response.ShouldBeProblem(StatusCodes.Status503ServiceUnavailable, "dependency_unavailable");
        response.ShouldNotContainOrderProjection(CommerceFixtures.OrderA, CommerceFixtures.OrderB);
        LicensingCallsFor(harness, CommerceFixtures.OrderA).Length.ShouldBe(1);
        harness.Recorder.Count("GetCustomerOrder", CommerceFixtures.OrderA).ShouldBe(surface == GatewaySurface.Detail ? 1 : 0);
        harness.Recorder.For("CreateOrder").Count.ShouldBe(surface == GatewaySurface.Replay ? 1 : 0);
    }

    /// <summary>The replay forwards the exact original basket revision and idempotency key once, with a nonpayable receipt.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task ReplayForwardsBoundIntentWithNullCheckoutUrl()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var script = new FakeCommerceScript()
            .Create(CreateOrderReply.Ok(ActiveA(), hasCheckoutUrl: false, checkoutUrl: null))
            .License(OrderAKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Ok(CommerceFixtures.Envelope()));
        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, GatewaySurface.Replay, token, cancellation).ConfigureAwait(true);

        response.ShouldBeActiveSuccess(GatewaySurface.Replay, response.Root.GetProperty("order"), CommerceFixtures.OrderA, CommerceFixtures.SubscriptionA, CommerceFixtures.PublicOrderRevision);
        var creates = harness.Recorder.For("CreateOrder");
        creates.Count.ShouldBe(1);
        creates[0].BasketRevision.ShouldBe(CommerceFixtures.ReplayBasketRevision);
        creates[0].IdempotencyKey.ShouldBe(CommerceFixtures.IdempotencyKey);
        creates[0].MetadataValue(TenantMetadataKey).ShouldBe(_keycloak.CustomerA.TenantId.ToString("D"));
        harness.Recorder.For("GetCustomerOrder").ShouldBeEmpty();
        LicensingCallsFor(harness, CommerceFixtures.OrderA).Length.ShouldBe(1);
    }

    /// <summary>An authenticated caller without commerce.customer is forbidden before any downstream call.</summary>
    /// <param name="surface">Customer HTTP surface under test.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData(GatewaySurface.Detail)]
    [InlineData(GatewaySurface.List)]
    [InlineData(GatewaySurface.Replay)]
    public async Task UnauthorizedCustomerIsDeniedBeforeDelegation(GatewaySurface surface)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await _keycloak.CustomerTokenWithoutCommercePermissionAsync(_keycloak.CustomerA, cancellation).ConfigureAwait(true);
        var script = ActiveScript(surface).License(OrderAKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Ok(CommerceFixtures.Envelope()));
        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, surface, token, cancellation).ConfigureAwait(true);

        response.ShouldBeProblem(StatusCodes.Status403Forbidden, "forbidden");
        response.ShouldNotContainOrderProjection(CommerceFixtures.OrderA, CommerceFixtures.OrderB);
        harness.Recorder.Calls.ShouldBeEmpty();
    }

    /// <summary>Caller-supplied tenant headers never override the tenant validated from the bearer token.</summary>
    /// <param name="surface">Customer HTTP surface under test.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData(GatewaySurface.Detail)]
    [InlineData(GatewaySurface.List)]
    [InlineData(GatewaySurface.Replay)]
    public async Task CallerTenantHeaderDoesNotChangeValidatedScope(GatewaySurface surface)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var spoofedTenant = new Guid(0x2b2b2b2b, 0x1111, 0x4222, 0x83, 0x33, 0x44, 0x44, 0x55, 0x55, 0x66, 0x66);
        var script = ActiveScript(surface).License(OrderAKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Ok(CommerceFixtures.Envelope()));
        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, surface, token, cancellation, request =>
        {
            request.Headers.Add(TenantMetadataKey, spoofedTenant.ToString("D"));
            request.Headers.Add("X-Tenant-Id", spoofedTenant.ToString("D"));
        }).ConfigureAwait(true);

        var expectedRevision = surface == GatewaySurface.List ? CommerceFixtures.ListOrderRevision : CommerceFixtures.PublicOrderRevision;
        response.ShouldBeActiveSuccess(surface, response.Order(surface, CommerceFixtures.OrderA), CommerceFixtures.OrderA, CommerceFixtures.SubscriptionA, expectedRevision);
        var licensing = LicensingCallsFor(harness, CommerceFixtures.OrderA);
        licensing.Length.ShouldBe(1);
        licensing[0].TenantId.ShouldBe(_keycloak.CustomerA.TenantId.ToString("D"));
        harness.Recorder.Calls.SelectMany(call => call.Metadata).Any(entry =>
            entry.Value.Contains(spoofedTenant.ToString("D"), StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
    }

    private static FakeCommerceScript ActiveScript(GatewaySurface surface)
    {
        var script = new FakeCommerceScript();
        switch (surface)
        {
            case GatewaySurface.Detail:
                _ = script.Order(OrderAKey, CustomerOrderReply.Ok(ActiveA()));
                break;
            case GatewaySurface.List:
                _ = script.Page(ListOrdersReply.Ok(
                    [ActiveA(CommerceFixtures.ListOrderRevision), ActiveB(CommerceFixtures.ListOrderRevision)],
                    CommerceFixtures.Cursor));
                _ = script.License(OrderBKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Ok(CommerceFixtures.Envelope()));
                break;
            case GatewaySurface.Replay:
                _ = script.Create(CreateOrderReply.Ok(ActiveA(), hasCheckoutUrl: false, checkoutUrl: null));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(surface));
        }

        return script;
    }

    private static FakeCommerceScript ScriptWithInitialTarget(GatewaySurface surface, CustomerOrder target)
    {
        var script = new FakeCommerceScript();
        switch (surface)
        {
            case GatewaySurface.Detail:
                _ = script.Order(OrderAKey, CustomerOrderReply.Ok(target));
                break;
            case GatewaySurface.List:
                _ = script.Page(ListOrdersReply.Ok([target, ActiveB(CommerceFixtures.ListOrderRevision)], CommerceFixtures.Cursor));
                _ = script.License(OrderBKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Ok(CommerceFixtures.Envelope()));
                break;
            case GatewaySurface.Replay:
                _ = script.Create(CreateOrderReply.Ok(target, hasCheckoutUrl: false, checkoutUrl: null));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(surface));
        }

        return script;
    }

    private static CustomerOrder ActiveA(
        uint revision = CommerceFixtures.PublicOrderRevision,
        uint? desiredRevision = CommerceFixtures.InitialDesiredRevision,
        DateTimeOffset? updatedAt = null) =>
        CommerceFixtures.ActiveOrder(CommerceFixtures.OrderA, CommerceFixtures.SubscriptionA, updatedAt ?? CommerceFixtures.AUpdatedAt, revision, desiredRevision);

    private static CustomerOrder ActiveB(
        uint revision = CommerceFixtures.ListOrderRevision,
        uint? desiredRevision = CommerceFixtures.InitialDesiredRevision,
        DateTimeOffset? updatedAt = null) =>
        CommerceFixtures.ActiveOrder(CommerceFixtures.OrderB, CommerceFixtures.SubscriptionB, updatedAt ?? CommerceFixtures.BUpdatedAt, revision, desiredRevision);

    private static CustomerOrder PendingReadinessA(DateTimeOffset updatedAt, uint revision = CommerceFixtures.PublicOrderRevision) =>
        CommerceFixtures.PendingReadinessOrder(CommerceFixtures.OrderA, CommerceFixtures.SubscriptionA, updatedAt, revision);

    private static RecordedCommerceCall[] LicensingCallsFor(GatewayHarness harness, Guid orderId)
    {
        var calls = harness.Recorder.For("GetCurrentLicense");
        return [.. calls.Where(call => string.Equals(call.OrderId, orderId.ToString("D"), StringComparison.Ordinal))];
    }

    private static async Task<CommerceHttpResponse> SendAsync(
        GatewayHarness harness,
        GatewaySurface surface,
        string token,
        CancellationToken cancellationToken,
        Action<HttpRequestMessage>? customize = null)
    {
        var message = await harness.SendAsync(surface, token, cancellationToken, customize: customize).ConfigureAwait(false);
        return await CommerceHttpResponse.ReadAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> CustomerTokenAsync(CancellationToken cancellationToken) =>
        (await _keycloak.CustomerTokenAsync(_keycloak.CustomerA, cancellationToken).ConfigureAwait(false)).AccessToken;

    private void ShouldTargetScope(RecordedCommerceCall call, Guid orderId, Guid subscriptionId, uint desiredRevision)
    {
        call.TenantId.ShouldBe(_keycloak.CustomerA.TenantId.ToString("D"));
        call.OrderId.ShouldBe(orderId.ToString("D"));
        call.SubscriptionId.ShouldBe(subscriptionId.ToString("D"));
        call.DesiredRevision.ShouldBe(desiredRevision);
        call.MetadataValue("authorization").ShouldNotBeNullOrWhiteSpace();
        call.MetadataValue(TenantMetadataKey).ShouldBe(_keycloak.CustomerA.TenantId.ToString("D"));
    }
}
