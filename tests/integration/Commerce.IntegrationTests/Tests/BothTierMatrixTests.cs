using System.Globalization;
using System.Text.Json.Nodes;
using Commerce.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace Commerce.IntegrationTests.Tests;

/// <summary>Real both-tier lifecycle through both gateways, Orders, Licensing, PostgreSQL and RabbitMQ.</summary>
/// <param name="keycloak">The shared disposable identity provider.</param>
[Collection(CommerceIntegrationSuite.Name)]
public sealed class BothTierMatrixTests(CommerceKeycloakFixture keycloak)
{
    /// <summary>Drives one configured tier from basket to a bound, verified license.</summary>
    /// <param name="tier">The selected tier.</param>
    /// <param name="replicaAddOn">Whether the dedicated replica add-on is selected.</param>
    /// <param name="expectedTotal">The immutable server price in minor units.</param>
    /// <returns>The lifecycle assertion task.</returns>
    [Theory]
    [InlineData("shared", false, 1000L)]
    [InlineData("dedicated", true, 3500L)]
    public async Task RealBothTierLifecycle(string tier, bool replicaAddOn, long expectedTotal)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var stack = await keycloak.StackAsync(cancellationToken).ConfigureAwait(true);
        var account = string.Equals(tier, "shared", StringComparison.Ordinal) ? keycloak.CustomerA : keycloak.CustomerB;
        var customerToken = (await keycloak.CustomerTokenAsync(account, cancellationToken).ConfigureAwait(true)).AccessToken;
        var staffToken = await keycloak.StaffTokenAsync(keycloak.StaffFull, cancellationToken).ConfigureAwait(true);
        using var customer = new CommerceHttp(stack.CustomerGatewayAddress);
        using var admin = new CommerceHttp(stack.AdminGatewayAddress);

        var actor = new LifecycleActor(stack, customer, admin, account, customerToken, staffToken);
        var checkout = await CreatePendingOrderAsync(actor, tier, replicaAddOn, expectedTotal, cancellationToken).ConfigureAwait(true);
        var paidThrough = stack.Stripe.Now.AddMonths(1);
        await MarkOrderPaidAsync(actor, checkout, paidThrough, cancellationToken).ConfigureAwait(true);
        var active = await ApproveAndActivateAsync(actor, checkout, tier, cancellationToken).ConfigureAwait(true);
        await AssertActiveLicenseAsync(actor, active, checkout, paidThrough, tier, replicaAddOn, cancellationToken).ConfigureAwait(true);
        await AssertReadyPlacementAsync(actor, checkout, tier, cancellationToken).ConfigureAwait(true);
    }

    private static async Task<Checkout> CreatePendingOrderAsync(
        LifecycleActor actor,
        string tier,
        bool replicaAddOn,
        long expectedTotal,
        CancellationToken cancellationToken)
    {
        var offerId = string.Equals(tier, "shared", StringComparison.Ordinal)
            ? CommerceTestConfiguration.SharedOfferId
            : CommerceTestConfiguration.DedicatedOfferId;
        string[] addOns = replicaAddOn ? [CommerceTestConfiguration.ReplicaAddOnCode] : [];

        var offer = await CommerceFlow.OfferAsync(actor.Customer, actor.CustomerToken, offerId, cancellationToken).ConfigureAwait(true);
        string.Equals((string?)offer["tier"], tier, StringComparison.Ordinal).ShouldBeTrue();
        if (replicaAddOn)
        {
            _ = offer["addOns"]!.AsArray().Single(addOn => string.Equals((string?)addOn!["code"], CommerceTestConfiguration.ReplicaAddOnCode, StringComparison.Ordinal));
        }
        else
        {
            offer["addOns"]!.AsArray().Count.ShouldBe(0);
        }

        var checkout = await CommerceFlow.CreatePayableOrderAsync(actor.Stack, actor.Customer, actor.CustomerToken, offerId, addOns, cancellationToken).ConfigureAwait(true);
        checkout.TotalMinor.ShouldBe(expectedTotal);
        checkout.CheckoutUrl.ShouldNotBeNull().ShouldStartWith("https://checkout.stripe.test/");

        var committedTotal = await Db.ScalarAsync(
            actor.Stack.Postgres.Orders.ConnectionString,
            "SELECT \"TotalAmountMinor\" FROM orders.\"Orders\" WHERE \"Id\" = $1",
            cancellationToken,
            checkout.OrderId).ConfigureAwait(true);
        committedTotal.ShouldBe(expectedTotal);

        var unpaidEvent = "evt_unpaid_" + Guid.NewGuid().ToString("N");
        await CommerceFlow.WebhookAsync(actor.Stack, unpaidEvent, "checkout.session.completed", checkout.OrderId, "checkout.session", cancellationToken).ConfigureAwait(true);
        await CommerceFlow.WaitReceiptCompletedAsync(actor.Stack, unpaidEvent, cancellationToken).ConfigureAwait(true);
        (await Db.ScalarAsync(
            actor.Stack.Postgres.Orders.ConnectionString,
            "SELECT \"PaymentStatus\" FROM orders.\"Orders\" WHERE \"Id\" = $1",
            cancellationToken,
            checkout.OrderId).ConfigureAwait(true)).ShouldBe(1);
        (await Db.TimestampAsync(
            actor.Stack.Postgres.Orders.ConnectionString,
            "SELECT \"PaidThrough\" FROM orders.\"Orders\" WHERE \"Id\" = $1",
            cancellationToken,
            checkout.OrderId).ConfigureAwait(true)).ShouldBeNull();

        var pendingAdmin = await CommerceFlow.AdminOrderAsync(actor.Admin, actor.StaffToken, actor.Account.TenantId, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        string.Equals((string?)pendingAdmin["payment"]!["status"], "pending", StringComparison.Ordinal).ShouldBeTrue();
        pendingAdmin["payment"]!["paidThrough"].ShouldBeNull();
        string.Equals((string?)pendingAdmin["status"], "pendingPayment", StringComparison.Ordinal).ShouldBeTrue();

        var unpaidCustomer = await CommerceFlow.CustomerOrderAsync(actor.Customer, actor.CustomerToken, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        string.Equals((string?)unpaidCustomer["status"], "pendingPayment", StringComparison.Ordinal).ShouldBeTrue();
        unpaidCustomer["license"].ShouldBeNull();
        return checkout;
    }

    private static async Task MarkOrderPaidAsync(
        LifecycleActor actor,
        Checkout checkout,
        DateTimeOffset paidThrough,
        CancellationToken cancellationToken)
    {
        actor.Stack.Stripe.MarkPaid(checkout.OrderId, paidThrough);
        var paidEvent = "evt_paid_" + Guid.NewGuid().ToString("N");
        await CommerceFlow.WebhookAsync(actor.Stack, paidEvent, "checkout.session.completed", checkout.OrderId, "checkout.session", cancellationToken).ConfigureAwait(true);
        await CommerceFlow.WaitReceiptCompletedAsync(actor.Stack, paidEvent, cancellationToken).ConfigureAwait(true);
        await CommerceFlow.WaitPaymentAsync(actor.Stack, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        var committedPaidThrough = await Db.TimestampAsync(
            actor.Stack.Postgres.Orders.ConnectionString,
            "SELECT \"PaidThrough\" FROM orders.\"Orders\" WHERE \"Id\" = $1",
            cancellationToken,
            checkout.OrderId).ConfigureAwait(true);
        _ = committedPaidThrough.ShouldNotBeNull();
        Math.Abs((committedPaidThrough.Value - paidThrough).TotalSeconds).ShouldBeLessThan(1);

        var paidAdmin = await CommerceFlow.AdminOrderAsync(actor.Admin, actor.StaffToken, actor.Account.TenantId, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        string.Equals((string?)paidAdmin["payment"]!["status"], "paid", StringComparison.Ordinal).ShouldBeTrue();
        var adminPaidThrough = DateTimeOffset.Parse(paidAdmin["payment"]!["paidThrough"]!.GetValue<string>(), CultureInfo.InvariantCulture);
        Math.Abs((adminPaidThrough - paidThrough).TotalSeconds).ShouldBeLessThan(1);
        string.Equals((string?)paidAdmin["status"], "pendingApproval", StringComparison.Ordinal).ShouldBeTrue();
        paidAdmin["license"].ShouldBeNull();
        var paidCustomer = await CommerceFlow.CustomerOrderAsync(actor.Customer, actor.CustomerToken, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        string.Equals((string?)paidCustomer["status"], "pendingApproval", StringComparison.Ordinal).ShouldBeTrue();
        paidCustomer["license"].ShouldBeNull();
        (await ActiveGrantCountAsync(actor.Stack, checkout.OrderId, cancellationToken).ConfigureAwait(true)).ShouldBe(0);
    }

    private static async Task<JsonObject> ApproveAndActivateAsync(
        LifecycleActor actor,
        Checkout checkout,
        string tier,
        CancellationToken cancellationToken)
    {
        var approved = await CommerceFlow.ApproveAsync(
            actor.Stack,
            actor.Admin,
            actor.StaffToken,
            actor.Account.TenantId,
            checkout.OrderId,
            "fixture-approved-" + tier,
            cancellationToken).ConfigureAwait(true);
        string.Equals((string?)approved["status"], "pendingReadiness", StringComparison.Ordinal).ShouldBeTrue();
        string.Equals((string?)approved["fulfillment"]!["status"], "awaitingEvidence", StringComparison.Ordinal).ShouldBeTrue();
        (await ActiveGrantCountAsync(actor.Stack, checkout.OrderId, cancellationToken).ConfigureAwait(true)).ShouldBe(0);

        _ = await CommerceFlow.RecordEvidenceAsync(
            actor.Stack,
            actor.Admin,
            actor.StaffToken,
            actor.Account.TenantId,
            checkout.OrderId,
            checkout.SubscriptionId,
            tier,
            "fixture-evidence-" + tier + "-" + Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(true);
        await actor.Stack.RestartOrdersAsync(cancellationToken).ConfigureAwait(true);
        return await CommerceFlow.WaitActiveAsync(
            actor.Stack,
            actor.Customer,
            actor.Admin,
            actor.CustomerToken,
            actor.StaffToken,
            actor.Account.TenantId,
            checkout.OrderId,
            cancellationToken).ConfigureAwait(true);
    }

    private static async Task AssertActiveLicenseAsync(
        LifecycleActor actor,
        JsonObject active,
        Checkout checkout,
        DateTimeOffset paidThrough,
        string tier,
        bool replicaAddOn,
        CancellationToken cancellationToken)
    {
        var license = active["license"].ShouldNotBeNull().AsObject();
        var document = license["document"]!.GetValue<string>();
        document.ShouldNotBeNullOrWhiteSpace();
        license["keyId"]!.GetValue<string>().ShouldBe("K1");

        var verified = await CommerceFlow.VerifyAsync(actor.Stack, document, actor.Account.TenantId, checkout.SubscriptionId, cancellationToken).ConfigureAwait(true);
        verified.Valid.ShouldBeTrue();
        verified.Claims.TenantId.ShouldBe(actor.Account.TenantId.ToString("D"));
        verified.Claims.OrderId.ShouldBe(checkout.OrderId.ToString("D"));
        verified.Claims.SubscriptionId.ShouldBe(checkout.SubscriptionId.ToString("D"));
        verified.Claims.Tier.ToString().ShouldBe(string.Equals(tier, "shared", StringComparison.Ordinal) ? "Shared" : "Dedicated");
        string[] addOns = replicaAddOn ? [CommerceTestConfiguration.ReplicaAddOnCode] : [];
        verified.Claims.AddOnCodes.Count.ShouldBe(addOns.Length);
        foreach (var code in addOns)
        {
            verified.Claims.AddOnCodes.ShouldContain(code, StringComparer.Ordinal);
        }

        var expiresAt = DateTimeOffset.Parse(license["expiresAt"]!.GetValue<string>(), CultureInfo.InvariantCulture);
        expiresAt.ShouldBeLessThanOrEqualTo(paidThrough.AddSeconds(1));
        var proofObservedAt = await Db.TimestampAsync(
            actor.Stack.Postgres.Orders.ConnectionString,
            "SELECT \"ProofObservedAt\" FROM orders.\"Orders\" WHERE \"Id\" = $1",
            cancellationToken,
            checkout.OrderId).ConfigureAwait(true);
        _ = proofObservedAt.ShouldNotBeNull();
        expiresAt.ShouldBeLessThanOrEqualTo(proofObservedAt.Value.AddMinutes(15).AddSeconds(1));
    }

    private static async Task AssertReadyPlacementAsync(
        LifecycleActor actor,
        Checkout checkout,
        string tier,
        CancellationToken cancellationToken)
    {
        var desiredRevision = await CommerceFlow.DesiredRevisionAsync(actor.Stack, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        var activeGrants = await ActiveGrantCountAsync(actor.Stack, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        activeGrants.ShouldBe(1);
        var grantRevision = await Db.ScalarAsync(
            actor.Stack.Postgres.Licensing.ConnectionString,
            "SELECT desired_revision FROM licensing.license_grants WHERE order_id = $1 AND state = 'active'",
            cancellationToken,
            checkout.OrderId).ConfigureAwait(true);
        grantRevision.ShouldBe(desiredRevision);

        var readyAdmin = await CommerceFlow.AdminOrderAsync(actor.Admin, actor.StaffToken, actor.Account.TenantId, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        string.Equals((string?)readyAdmin["fulfillment"]!["status"], "ready", StringComparison.Ordinal).ShouldBeTrue();
        readyAdmin["fulfillment"]!["failureCode"].ShouldBeNull();
        readyAdmin["fulfillment"]!["placementId"]!.GetValue<string>().ShouldBe(
            (string.Equals(tier, "dedicated", StringComparison.Ordinal)
                ? CommerceTestConfiguration.DedicatedPlacementId
                : CommerceTestConfiguration.SharedPlacementId).ToString("D"));
        var auditActions = readyAdmin["audit"]!.AsArray().Select(entry => (string?)entry!["action"]).ToArray();
        auditActions.ShouldContain("approve", StringComparer.Ordinal);
        auditActions.ShouldContain("recordPlacementEvidence", StringComparer.Ordinal);
    }

    private static Task<long> ActiveGrantCountAsync(CommerceStack stack, Guid orderId, CancellationToken cancellationToken) =>
        Db.ScalarAsync(
            stack.Postgres.Licensing.ConnectionString,
            "SELECT count(*) FROM licensing.license_grants WHERE order_id = $1 AND state = 'active' AND is_deleted = false",
            cancellationToken,
            orderId);

    private sealed record LifecycleActor(
        CommerceStack Stack,
        CommerceHttp Customer,
        CommerceHttp Admin,
        CustomerAccount Account,
        string CustomerToken,
        string StaffToken);
}
