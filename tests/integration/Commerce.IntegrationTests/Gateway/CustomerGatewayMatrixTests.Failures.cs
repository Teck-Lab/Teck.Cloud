using System.Net;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Failure and generation-fence behavior of the real customer gateway HTTP pipeline.</summary>
public sealed partial class CustomerGatewayMatrixTests
{
    /// <summary>A valid initial Licensing PERMISSION_DENIED stays an ordinary caller 403 with no reread.</summary>
    /// <param name="surface">Customer HTTP surface under test.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData(GatewaySurface.Detail)]
    [InlineData(GatewaySurface.List)]
    [InlineData(GatewaySurface.Replay)]
    public async Task InitialLicensingForbiddenKeepsOrdinaryAuthorization(GatewaySurface surface)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var script = ActiveScript(surface).License(
            OrderAKey,
            CommerceFixtures.InitialDesiredRevision,
            LicenseReply.Error(StatusCode.PermissionDenied, "forbidden"));
        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, surface, token, cancellation).ConfigureAwait(true);

        response.ShouldBeProblem(StatusCodes.Status403Forbidden, "forbidden");
        response.ShouldNotContainOrderProjection(CommerceFixtures.OrderA, CommerceFixtures.OrderB);
        var licensing = LicensingCallsFor(harness, CommerceFixtures.OrderA);
        licensing.Length.ShouldBe(1);
        ShouldTargetScope(licensing[0], CommerceFixtures.OrderA, CommerceFixtures.SubscriptionA, CommerceFixtures.InitialDesiredRevision);
        harness.Recorder.Count("GetCustomerOrder", CommerceFixtures.OrderA).ShouldBe(surface == GatewaySurface.Detail ? 1 : 0);
        LicensingCallsFor(harness, CommerceFixtures.OrderA).Any(call => call.DesiredRevision == CommerceFixtures.RereadDesiredRevision).ShouldBeFalse();
        harness.Recorder.For("CreateOrder").Count.ShouldBe(surface == GatewaySurface.Replay ? 1 : 0);
    }

    /// <summary>The single same-binding Orders reread keeps ordinary 403, scoped 404 and transport 503 behavior.</summary>
    /// <param name="surface">Customer HTTP surface under test.</param>
    /// <param name="failure">Reread failure mode.</param>
    /// <returns>The verification task.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="failure"/> is not a defined reread failure.</exception>
    [Theory]
    [InlineData(GatewaySurface.Detail, OrdersRereadFailure.Forbidden)]
    [InlineData(GatewaySurface.Detail, OrdersRereadFailure.NotFound)]
    [InlineData(GatewaySurface.Detail, OrdersRereadFailure.Transport)]
    [InlineData(GatewaySurface.List, OrdersRereadFailure.Forbidden)]
    [InlineData(GatewaySurface.List, OrdersRereadFailure.NotFound)]
    [InlineData(GatewaySurface.List, OrdersRereadFailure.Transport)]
    [InlineData(GatewaySurface.Replay, OrdersRereadFailure.Forbidden)]
    [InlineData(GatewaySurface.Replay, OrdersRereadFailure.NotFound)]
    [InlineData(GatewaySurface.Replay, OrdersRereadFailure.Transport)]
    public async Task OrdersRereadFailureKeepsOrdinaryMappingWithoutConfirmation(GatewaySurface surface, OrdersRereadFailure failure)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var rereadReply = failure switch
        {
            OrdersRereadFailure.Forbidden => CustomerOrderReply.Error(StatusCode.PermissionDenied, "forbidden"),
            OrdersRereadFailure.NotFound => CustomerOrderReply.Error(StatusCode.NotFound, "not_found"),
            OrdersRereadFailure.Transport => CustomerOrderReply.TransportFailure(),
            _ => throw new ArgumentOutOfRangeException(nameof(failure)),
        };
        var script = ActiveScript(surface)
            .License(OrderAKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Error(StatusCode.NotFound, "not_found"))
            .Order(OrderAKey, rereadReply);
        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, surface, token, cancellation).ConfigureAwait(true);

        switch (failure)
        {
            case OrdersRereadFailure.Forbidden:
                response.ShouldBeProblem(StatusCodes.Status403Forbidden, "forbidden");
                break;
            case OrdersRereadFailure.NotFound:
                response.ShouldBeProblem(StatusCodes.Status404NotFound, "not_found");
                break;
            case OrdersRereadFailure.Transport:
                response.ShouldBeProblem(StatusCodes.Status503ServiceUnavailable, "dependency_unavailable");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failure));
        }

        response.ShouldNotContainOrderProjection(CommerceFixtures.OrderA, CommerceFixtures.OrderB);
        var licensing = LicensingCallsFor(harness, CommerceFixtures.OrderA);
        licensing.Length.ShouldBe(1);
        licensing[0].DesiredRevision.ShouldBe(CommerceFixtures.InitialDesiredRevision);
        harness.Recorder.Count("GetCustomerOrder", CommerceFixtures.OrderA).ShouldBe(surface == GatewaySurface.Detail ? 2 : 1);
        harness.Recorder.For("CreateOrder").Count.ShouldBe(surface == GatewaySurface.Replay ? 1 : 0);
    }

    /// <summary>Malformed initial Licensing trailers fail closed without the initial-only reread.</summary>
    /// <param name="surface">Customer HTTP surface under test.</param>
    /// <param name="defect">Malformed trailer variant.</param>
    /// <returns>The verification task.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="defect"/> is not a defined trailer defect.</exception>
    [Theory]
    [InlineData(GatewaySurface.Detail, TrailerDefect.Missing)]
    [InlineData(GatewaySurface.Detail, TrailerDefect.Duplicate)]
    [InlineData(GatewaySurface.Detail, TrailerDefect.StatusMismatch)]
    [InlineData(GatewaySurface.Detail, TrailerDefect.UnknownCode)]
    [InlineData(GatewaySurface.Detail, TrailerDefect.Malformed)]
    [InlineData(GatewaySurface.List, TrailerDefect.Missing)]
    [InlineData(GatewaySurface.List, TrailerDefect.Duplicate)]
    [InlineData(GatewaySurface.List, TrailerDefect.StatusMismatch)]
    [InlineData(GatewaySurface.List, TrailerDefect.UnknownCode)]
    [InlineData(GatewaySurface.List, TrailerDefect.Malformed)]
    [InlineData(GatewaySurface.Replay, TrailerDefect.Missing)]
    [InlineData(GatewaySurface.Replay, TrailerDefect.Duplicate)]
    [InlineData(GatewaySurface.Replay, TrailerDefect.StatusMismatch)]
    [InlineData(GatewaySurface.Replay, TrailerDefect.UnknownCode)]
    [InlineData(GatewaySurface.Replay, TrailerDefect.Malformed)]
    public async Task InvalidInitialLicensingTrailersFailClosedWithoutReread(GatewaySurface surface, TrailerDefect defect)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var defectReply = defect switch
        {
            TrailerDefect.Missing => LicenseReply.Mute(StatusCode.NotFound),
            TrailerDefect.Duplicate => LicenseReply.Error(StatusCode.NotFound, "not_found", "not_found"),
            TrailerDefect.StatusMismatch => LicenseReply.Error(StatusCode.InvalidArgument, "not_found"),
            TrailerDefect.UnknownCode => LicenseReply.Error(StatusCode.InvalidArgument, "unknown_code"),
            TrailerDefect.Malformed => LicenseReply.Error(StatusCode.InvalidArgument, "INVALID_REQUEST"),
            _ => throw new ArgumentOutOfRangeException(nameof(defect)),
        };
        var script = ActiveScript(surface)
            .License(OrderAKey, CommerceFixtures.InitialDesiredRevision, defectReply);
        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, surface, token, cancellation).ConfigureAwait(true);

        response.ShouldBeProblem(StatusCodes.Status503ServiceUnavailable, "dependency_unavailable");
        response.ShouldNotContainOrderProjection(CommerceFixtures.OrderA, CommerceFixtures.OrderB);
        LicensingCallsFor(harness, CommerceFixtures.OrderA).Length.ShouldBe(1);
        harness.Recorder.Count("GetCustomerOrder", CommerceFixtures.OrderA).ShouldBe(surface == GatewaySurface.Detail ? 1 : 0);
        harness.Recorder.For("CreateOrder").Count.ShouldBe(surface == GatewaySurface.Replay ? 1 : 0);
    }

    /// <summary>Absent or zero active desired generation fails closed at the initial snapshot and at the reread.</summary>
    /// <param name="surface">Customer HTTP surface under test.</param>
    /// <param name="snapshot">Snapshot carrying the invalid generation.</param>
    /// <param name="defect">Absent or zero encoding.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData(GatewaySurface.Detail, GenerationSnapshot.Initial, GenerationDefect.Absent)]
    [InlineData(GatewaySurface.Detail, GenerationSnapshot.Initial, GenerationDefect.Zero)]
    [InlineData(GatewaySurface.Detail, GenerationSnapshot.Reread, GenerationDefect.Absent)]
    [InlineData(GatewaySurface.Detail, GenerationSnapshot.Reread, GenerationDefect.Zero)]
    [InlineData(GatewaySurface.List, GenerationSnapshot.Initial, GenerationDefect.Absent)]
    [InlineData(GatewaySurface.List, GenerationSnapshot.Initial, GenerationDefect.Zero)]
    [InlineData(GatewaySurface.List, GenerationSnapshot.Reread, GenerationDefect.Absent)]
    [InlineData(GatewaySurface.List, GenerationSnapshot.Reread, GenerationDefect.Zero)]
    [InlineData(GatewaySurface.Replay, GenerationSnapshot.Initial, GenerationDefect.Absent)]
    [InlineData(GatewaySurface.Replay, GenerationSnapshot.Initial, GenerationDefect.Zero)]
    [InlineData(GatewaySurface.Replay, GenerationSnapshot.Reread, GenerationDefect.Absent)]
    [InlineData(GatewaySurface.Replay, GenerationSnapshot.Reread, GenerationDefect.Zero)]
    public async Task InvalidActiveDesiredGenerationFailsClosedWithoutLicensingLookup(
        GatewaySurface surface,
        GenerationSnapshot snapshot,
        GenerationDefect defect)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        uint? generation = defect == GenerationDefect.Absent ? null : 0u;
        var invalid = CommerceFixtures.ActiveOrder(
            CommerceFixtures.OrderA,
            CommerceFixtures.SubscriptionA,
            CommerceFixtures.AUpdatedAt,
            CommerceFixtures.PublicOrderRevision,
            generation);
        var script = snapshot == GenerationSnapshot.Initial ? ScriptWithInitialTarget(surface, invalid) : ActiveScript(surface);
        if (snapshot == GenerationSnapshot.Reread)
        {
            _ = script
                .License(OrderAKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Error(StatusCode.NotFound, "not_found"))
                .Order(OrderAKey, CustomerOrderReply.Ok(invalid));
        }

        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, surface, token, cancellation).ConfigureAwait(true);

        response.ShouldBeProblem(StatusCodes.Status503ServiceUnavailable, "dependency_unavailable");
        response.ShouldNotContainOrderProjection(CommerceFixtures.OrderA, CommerceFixtures.OrderB);
        var licensing = LicensingCallsFor(harness, CommerceFixtures.OrderA);
        if (snapshot == GenerationSnapshot.Initial)
        {
            licensing.ShouldBeEmpty();
            harness.Recorder.Count("GetCustomerOrder", CommerceFixtures.OrderA).ShouldBe(surface == GatewaySurface.Detail ? 1 : 0);
        }
        else
        {
            licensing.Length.ShouldBe(1);
            licensing[0].DesiredRevision.ShouldBe(CommerceFixtures.InitialDesiredRevision);
            harness.Recorder.Count("GetCustomerOrder", CommerceFixtures.OrderA).ShouldBe(surface == GatewaySurface.Detail ? 2 : 1);
        }

        harness.Recorder.For("CreateOrder").Count.ShouldBe(surface == GatewaySurface.Replay ? 1 : 0);
    }

    /// <summary>A Licensing transport failure issues no Orders reread.</summary>
    /// <param name="surface">Customer HTTP surface under test.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData(GatewaySurface.Detail)]
    [InlineData(GatewaySurface.List)]
    [InlineData(GatewaySurface.Replay)]
    public async Task InitialLicensingTransportFailureIssuesNoReread(GatewaySurface surface)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var script = ActiveScript(surface).License(OrderAKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.TransportFailure());
        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, surface, token, cancellation).ConfigureAwait(true);

        response.ShouldBeProblem(StatusCodes.Status503ServiceUnavailable, "dependency_unavailable");
        response.ShouldNotContainOrderProjection(CommerceFixtures.OrderA, CommerceFixtures.OrderB);
        LicensingCallsFor(harness, CommerceFixtures.OrderA).Length.ShouldBe(1);
        harness.Recorder.Count("GetCustomerOrder", CommerceFixtures.OrderA).ShouldBe(surface == GatewaySurface.Detail ? 1 : 0);
        harness.Recorder.For("CreateOrder").Count.ShouldBe(surface == GatewaySurface.Replay ? 1 : 0);
    }

    /// <summary>An initial Orders transport failure returns 503 without any Licensing lookup.</summary>
    /// <param name="surface">Customer HTTP surface under test.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData(GatewaySurface.Detail)]
    [InlineData(GatewaySurface.List)]
    [InlineData(GatewaySurface.Replay)]
    public async Task InitialOrdersTransportFailureFailsClosed(GatewaySurface surface)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var script = InitialTransportScript(surface);
        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, surface, token, cancellation).ConfigureAwait(true);

        response.ShouldBeProblem(StatusCodes.Status503ServiceUnavailable, "dependency_unavailable");
        response.ShouldNotContainOrderProjection(CommerceFixtures.OrderA, CommerceFixtures.OrderB);
        harness.Recorder.For("GetCurrentLicense").ShouldBeEmpty();
        harness.Recorder.For("CreateOrder").Count.ShouldBe(surface == GatewaySurface.Replay ? 1 : 0);
    }

    /// <summary>A still-active reread that is now nonactive emits the latest projection with no license and no confirmation.</summary>
    /// <param name="surface">Customer HTTP surface under test.</param>
    /// <returns>The verification task.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="surface"/> is not a defined gateway surface.</exception>
    [Theory]
    [InlineData(GatewaySurface.Detail)]
    [InlineData(GatewaySurface.List)]
    [InlineData(GatewaySurface.Replay)]
    public async Task NonactiveRereadReturnsLatestProjectionWithoutConfirmation(GatewaySurface surface)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var script = ActiveScript(surface)
            .License(OrderAKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Error(StatusCode.NotFound, "not_found"))
            .Order(OrderAKey, CustomerOrderReply.Ok(PendingReadinessA(CommerceFixtures.BRereadAt, CommerceFixtures.PublicOrderRevision)));
        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, surface, token, cancellation).ConfigureAwait(true);

        switch (surface)
        {
            case GatewaySurface.Detail:
                response.Status.ShouldBe(HttpStatusCode.OK);
                response.MediaType.ShouldBe("application/json");
                response.Root.ShouldBePendingReadiness(CommerceFixtures.PublicOrderRevision, CommerceFixtures.BRereadAt);
                break;
            case GatewaySurface.List:
                response.ShouldBeListPage([OrderAKey, OrderBKey], CommerceFixtures.Cursor);
                response.Order(surface, CommerceFixtures.OrderA).ShouldBePendingReadiness(CommerceFixtures.PublicOrderRevision, CommerceFixtures.BRereadAt);
                break;
            case GatewaySurface.Replay:
                response.Status.ShouldBe(HttpStatusCode.Created);
                response.Root.GetProperty("checkoutUrl").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
                response.Root.GetProperty("order").ShouldBePendingReadiness(CommerceFixtures.PublicOrderRevision, CommerceFixtures.BRereadAt);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(surface));
        }

        var licensing = LicensingCallsFor(harness, CommerceFixtures.OrderA);
        licensing.Length.ShouldBe(1);
        licensing[0].DesiredRevision.ShouldBe(CommerceFixtures.InitialDesiredRevision);
        harness.Recorder.Count("GetCustomerOrder", CommerceFixtures.OrderA).ShouldBe(surface == GatewaySurface.Detail ? 2 : 1);
        harness.Recorder.For("CreateOrder").Count.ShouldBe(surface == GatewaySurface.Replay ? 1 : 0);
    }

    /// <summary>An empty or trailered OK on the generation-8 confirmation also fails the whole response.</summary>
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
    public async Task MalformedConfirmationSuccessFailsClosed(GatewaySurface surface, MalformedLicenseSuccess success)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var confirmationReply = success switch
        {
            MalformedLicenseSuccess.EmptyEnvelope => LicenseReply.Empty(),
            MalformedLicenseSuccess.TraileredOk => LicenseReply.Ok(CommerceFixtures.Envelope(), "forbidden"),
            _ => throw new ArgumentOutOfRangeException(nameof(success)),
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

    /// <summary>Malformed initial Orders trailers also fail closed before any Licensing lookup.</summary>
    /// <param name="surface">Customer HTTP surface under test.</param>
    /// <param name="defect">Missing or mismatched frozen trailer.</param>
    /// <returns>The verification task.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="surface"/> is not a defined gateway surface.</exception>
    [Theory]
    [InlineData(GatewaySurface.Detail, TrailerDefect.Missing)]
    [InlineData(GatewaySurface.Detail, TrailerDefect.StatusMismatch)]
    [InlineData(GatewaySurface.List, TrailerDefect.Missing)]
    [InlineData(GatewaySurface.List, TrailerDefect.StatusMismatch)]
    [InlineData(GatewaySurface.Replay, TrailerDefect.Missing)]
    [InlineData(GatewaySurface.Replay, TrailerDefect.StatusMismatch)]
    public async Task InitialOrdersMalformedTrailerFailsClosed(GatewaySurface surface, TrailerDefect defect)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var failure = defect == TrailerDefect.Missing
            ? DependencyFailure.MissingTrailer(StatusCode.NotFound)
            : DependencyFailure.Trailered(StatusCode.PermissionDenied, "not_found");
        var script = new FakeCommerceScript();
        _ = surface switch
        {
            GatewaySurface.Detail => script.Order(OrderAKey, CustomerOrderReply.Failure(failure)),
            GatewaySurface.List => script.Page(ListOrdersReply.Failure(failure)),
            GatewaySurface.Replay => script.Create(CreateOrderReply.Failure(failure)),
            _ => throw new ArgumentOutOfRangeException(nameof(surface)),
        };

        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, surface, token, cancellation).ConfigureAwait(true);

        response.ShouldBeProblem(StatusCodes.Status503ServiceUnavailable, "dependency_unavailable");
        response.ShouldNotContainOrderProjection(CommerceFixtures.OrderA, CommerceFixtures.OrderB);
        harness.Recorder.For("GetCurrentLicense").ShouldBeEmpty();
        harness.Recorder.For("CreateOrder").Count.ShouldBe(surface == GatewaySurface.Replay ? 1 : 0);
    }

    /// <summary>A malformed Orders reread trailer fails closed without a confirmation lookup.</summary>
    /// <param name="surface">Customer HTTP surface under test.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData(GatewaySurface.Detail)]
    [InlineData(GatewaySurface.List)]
    [InlineData(GatewaySurface.Replay)]
    public async Task MalformedOrdersRereadTrailerFailsClosedWithoutConfirmation(GatewaySurface surface)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await CustomerTokenAsync(cancellation).ConfigureAwait(true);
        var script = ActiveScript(surface)
            .License(OrderAKey, CommerceFixtures.InitialDesiredRevision, LicenseReply.Error(StatusCode.NotFound, "not_found"))
            .Order(OrderAKey, CustomerOrderReply.Failure(DependencyFailure.Trailered(StatusCode.PermissionDenied, "not_found")));
        await using var harness = await GatewayHarness.StartAsync(script, _keycloak, cancellation).ConfigureAwait(true);
        using var response = await SendAsync(harness, surface, token, cancellation).ConfigureAwait(true);

        response.ShouldBeProblem(StatusCodes.Status503ServiceUnavailable, "dependency_unavailable");
        response.ShouldNotContainOrderProjection(CommerceFixtures.OrderA, CommerceFixtures.OrderB);
        var licensing = LicensingCallsFor(harness, CommerceFixtures.OrderA);
        licensing.Length.ShouldBe(1);
        licensing[0].DesiredRevision.ShouldBe(CommerceFixtures.InitialDesiredRevision);
        harness.Recorder.Count("GetCustomerOrder", CommerceFixtures.OrderA).ShouldBe(surface == GatewaySurface.Detail ? 2 : 1);
        harness.Recorder.For("CreateOrder").Count.ShouldBe(surface == GatewaySurface.Replay ? 1 : 0);
    }

    private static FakeCommerceScript InitialTransportScript(GatewaySurface surface)
    {
        var script = new FakeCommerceScript();
        _ = surface switch
        {
            GatewaySurface.Detail => script.Order(OrderAKey, CustomerOrderReply.TransportFailure()),
            GatewaySurface.List => script.Page(ListOrdersReply.TransportFailure()),
            GatewaySurface.Replay => script.Create(CreateOrderReply.TransportFailure()),
            _ => throw new ArgumentOutOfRangeException(nameof(surface)),
        };

        return script;
    }
}
