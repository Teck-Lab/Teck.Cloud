using System.Net;
using System.Text.Json.Nodes;
using Commerce.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace Commerce.IntegrationTests.Tests;

/// <summary>Durable checkout, replay, reordering and generation fences over real infrastructure.</summary>
/// <param name="keycloak">The shared disposable identity provider.</param>
[Collection(CommerceIntegrationSuite.Name)]
public sealed class DurableCheckoutTests(CommerceKeycloakFixture keycloak)
{
    /// <summary>One intent survives concurrency, restart and basket replacement.</summary>
    /// <returns>The durability assertion task.</returns>
    [Fact]
    public async Task ConcurrentRestartAndBasketReplacementKeepOneIntent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var stack = await keycloak.StackAsync(cancellationToken).ConfigureAwait(true);
        var token = (await keycloak.CustomerTokenAsync(keycloak.CustomerA, cancellationToken).ConfigureAwait(true)).AccessToken;
        using var customer = new CommerceHttp(stack.CustomerGatewayAddress);
        var basket = await CommerceFlow.PutBasketAsync(
            customer,
            token,
            CommerceTestConfiguration.SharedOfferId,
            [],
            cancellationToken).ConfigureAwait(true);
        var body = new JsonObject { ["basketRevision"] = basket.Revision };
        var headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Idempotency-Key"] = Guid.NewGuid().ToString("D") };

        var orderId = await CreateConcurrentIntentAsync(customer, token, body, headers, cancellationToken).ConfigureAwait(true);
        await BoundedWait.UntilAsync(
            _ => Task.FromResult(stack.Stripe.CreateCount(orderId) == 1),
            TimeSpan.FromSeconds(20),
            "Concurrent identical intents created more than one provider session.",
            cancellationToken).ConfigureAwait(true);
        stack.Stripe.State(orderId).CreateCount.ShouldBe(1);
        (await Db.ScalarAsync(
            stack.Postgres.Orders.ConnectionString,
            "SELECT count(*) FROM orders.\"IdempotencyReceipts\" WHERE \"OrderId\" = $1",
            cancellationToken,
            orderId).ConfigureAwait(true)).ShouldBe(1);

        await stack.RestartOrdersAsync(cancellationToken).ConfigureAwait(true);
        var replay = await customer.PostAsync("/v1/commerce/orders", token, body, cancellationToken, headers).ConfigureAwait(true);
        replay.Status.ShouldBe(HttpStatusCode.Created, replay.Body);
        Guid.Parse(replay.Json()["order"]!["orderId"]!.GetValue<string>()).ShouldBe(orderId);
        stack.Stripe.State(orderId).CreateCount.ShouldBe(1);

        await AssertIdempotencyConflictsAsync(customer, token, body, headers, basket.Revision + 1, cancellationToken).ConfigureAwait(true);
        await AssertEmptyBasketRejectsOrderAsync(customer, token, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Reordered and duplicated provider notifications cannot regress a paid period.</summary>
    /// <returns>The reordering assertion task.</returns>
    [Fact]
    public async Task PaidAndUnpaidNotificationsNeverRegressCurrentState()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var stack = await keycloak.StackAsync(cancellationToken).ConfigureAwait(true);
        var token = (await keycloak.CustomerTokenAsync(keycloak.CustomerA, cancellationToken).ConfigureAwait(true)).AccessToken;
        using var customer = new CommerceHttp(stack.CustomerGatewayAddress);
        var checkout = await CommerceFlow.CreatePayableOrderAsync(
            stack,
            customer,
            token,
            CommerceTestConfiguration.SharedOfferId,
            [],
            cancellationToken).ConfigureAwait(true);
        stack.Stripe.MarkPaid(checkout.OrderId, stack.Stripe.Now.AddMonths(1));
        var paidEvent = "evt_reorder_paid_" + checkout.OrderId.ToString("N");
        await CommerceFlow.WebhookAsync(stack, paidEvent, "checkout.session.completed", checkout.OrderId, "checkout.session", cancellationToken).ConfigureAwait(true);
        await CommerceFlow.WaitReceiptCompletedAsync(stack, paidEvent, cancellationToken).ConfigureAwait(true);
        await CommerceFlow.WaitPaymentAsync(stack, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        var paidThrough = await Db.TimestampAsync(
            stack.Postgres.Orders.ConnectionString,
            "SELECT \"PaidThrough\" FROM orders.\"Orders\" WHERE \"Id\" = $1",
            cancellationToken,
            checkout.OrderId).ConfigureAwait(true);

        stack.Stripe.MarkUnpaid(checkout.OrderId);
        var unpaidEvent = "evt_reorder_unpaid_" + checkout.OrderId.ToString("N");
        await CommerceFlow.WebhookAsync(stack, unpaidEvent, "checkout.session.completed", checkout.OrderId, "checkout.session", cancellationToken).ConfigureAwait(true);
        await CommerceFlow.WaitReceiptCompletedAsync(stack, unpaidEvent, cancellationToken).ConfigureAwait(true);
        (await Db.ScalarAsync(
            stack.Postgres.Orders.ConnectionString,
            "SELECT \"PaymentStatus\" FROM orders.\"Orders\" WHERE \"Id\" = $1",
            cancellationToken,
            checkout.OrderId).ConfigureAwait(true)).ShouldBe(2);
        (await Db.TimestampAsync(
            stack.Postgres.Orders.ConnectionString,
            "SELECT \"PaidThrough\" FROM orders.\"Orders\" WHERE \"Id\" = $1",
            cancellationToken,
            checkout.OrderId).ConfigureAwait(true)).ShouldBe(paidThrough);

        using var webhook = new CommerceHttp(stack.OrdersWebhookAddress);
        var replay = await webhook.PostRawAsync(
            CommerceFlow.WebhookPath,
            stack.Stripe.WebhookPayload(paidEvent, "checkout.session.completed", checkout.OrderId, "checkout.session"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Stripe-Signature"] = stack.Stripe.SignatureHeader(
                    stack.Stripe.WebhookPayload(paidEvent, "checkout.session.completed", checkout.OrderId, "checkout.session"),
                    DateTimeOffset.UtcNow),
            },
            cancellationToken).ConfigureAwait(true);
        replay.Status.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Accepted, HttpStatusCode.NoContent);
        (await Db.ScalarAsync(
            stack.Postgres.Orders.ConnectionString,
            "SELECT count(*) FROM orders.\"ProviderEventReceipts\" WHERE \"EventId\" = $1",
            cancellationToken,
            paidEvent).ConfigureAwait(true)).ShouldBe(1);
    }

    /// <summary>Replacing ready evidence immediately invalidates the previous online license.</summary>
    /// <returns>The fenced-evidence assertion task.</returns>
    [Fact]
    public async Task EvidenceReplacementImmediatelyFailsOnlineLicenseVerification()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var stack = await keycloak.StackAsync(cancellationToken).ConfigureAwait(true);
        var customerToken = (await keycloak.CustomerTokenAsync(keycloak.CustomerA, cancellationToken).ConfigureAwait(true)).AccessToken;
        var staffToken = await keycloak.StaffTokenAsync(keycloak.StaffFull, cancellationToken).ConfigureAwait(true);
        using var customer = new CommerceHttp(stack.CustomerGatewayAddress);
        using var admin = new CommerceHttp(stack.AdminGatewayAddress);
        var checkout = await CommerceFlow.ActivateAsync(
            stack,
            customer,
            admin,
            keycloak.CustomerA,
            customerToken,
            staffToken,
            "shared",
            [],
            cancellationToken).ConfigureAwait(true);
        var active = await CommerceFlow.CustomerOrderAsync(customer, customerToken, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        var document = active["license"]!["document"]!.GetValue<string>();
        var valid = await CommerceFlow.VerifyAsync(stack, document, keycloak.CustomerA.TenantId, checkout.SubscriptionId, cancellationToken).ConfigureAwait(true);
        valid.Valid.ShouldBeTrue();

        _ = await CommerceFlow.RecordEvidenceAsync(
            stack,
            admin,
            staffToken,
            keycloak.CustomerA.TenantId,
            checkout.OrderId,
            checkout.SubscriptionId,
            "shared",
            "replacement-evidence-" + Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(true);
        var revoked = await CommerceFlow.VerifyAsync(stack, document, keycloak.CustomerA.TenantId, checkout.SubscriptionId, cancellationToken).ConfigureAwait(true);
        revoked.Valid.ShouldBeFalse();
        revoked.Rejection.ShouldBe(Teck.Cloud.Commerce.Licensing.V1.RejectionCode.Revoked);
        var current = await CommerceFlow.CustomerOrderAsync(customer, customerToken, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        if (string.Equals((string?)current["status"], "active", StringComparison.Ordinal))
        {
            current["license"]!["document"]!.GetValue<string>().ShouldNotBe(document, StringComparer.Ordinal);
        }
        else
        {
            current["license"].ShouldBeNull();
        }
    }

    /// <summary>Unpaid, rejected and unready orders never receive a license.</summary>
    /// <returns>The negative-gate assertion task.</returns>
    [Fact]
    public async Task UnpaidRejectedAndUnreadyOrdersNeverActivate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var stack = await keycloak.StackAsync(cancellationToken).ConfigureAwait(true);
        var staffToken = await keycloak.StaffTokenAsync(keycloak.StaffFull, cancellationToken).ConfigureAwait(true);
        using var customer = new CommerceHttp(stack.CustomerGatewayAddress);
        using var admin = new CommerceHttp(stack.AdminGatewayAddress);

        var unpaidToken = (await keycloak.CustomerTokenAsync(keycloak.CustomerA, cancellationToken).ConfigureAwait(true)).AccessToken;
        var unpaid = await CommerceFlow.CreatePayableOrderAsync(stack, customer, unpaidToken, CommerceTestConfiguration.SharedOfferId, [], cancellationToken).ConfigureAwait(true);
        var unpaidEvent = "evt_unpaid_negative_" + unpaid.OrderId.ToString("N");
        await CommerceFlow.WebhookAsync(stack, unpaidEvent, "checkout.session.completed", unpaid.OrderId, "checkout.session", cancellationToken).ConfigureAwait(true);
        await CommerceFlow.WaitReceiptCompletedAsync(stack, unpaidEvent, cancellationToken).ConfigureAwait(true);
        await AssertNoLicenseAsync(stack, customer, admin, keycloak.StaffFull, unpaidToken, staffToken, keycloak.CustomerA.TenantId, unpaid.OrderId, "pendingPayment", cancellationToken).ConfigureAwait(true);

        var rejectedToken = (await keycloak.CustomerTokenAsync(keycloak.CustomerB, cancellationToken).ConfigureAwait(true)).AccessToken;
        var rejected = await CommerceFlow.CreatePayableOrderAsync(stack, customer, rejectedToken, CommerceTestConfiguration.SharedOfferId, [], cancellationToken).ConfigureAwait(true);
        stack.Stripe.MarkPaid(rejected.OrderId, stack.Stripe.Now.AddMonths(1));
        var rejectedEvent = "evt_rejected_" + rejected.OrderId.ToString("N");
        await CommerceFlow.WebhookAsync(stack, rejectedEvent, "checkout.session.completed", rejected.OrderId, "checkout.session", cancellationToken).ConfigureAwait(true);
        await CommerceFlow.WaitReceiptCompletedAsync(stack, rejectedEvent, cancellationToken).ConfigureAwait(true);
        await CommerceFlow.WaitPaymentAsync(stack, rejected.OrderId, cancellationToken).ConfigureAwait(true);
        var adminOrder = await CommerceFlow.AdminOrderAsync(admin, staffToken, keycloak.CustomerB.TenantId, rejected.OrderId, cancellationToken).ConfigureAwait(true);
        var decision = await CommerceFlow.AdminDecisionAsync(
            admin,
            staffToken,
            keycloak.CustomerB.TenantId,
            rejected.OrderId,
            "reject",
            "fixture-rejected",
            adminOrder["revision"]!.GetValue<uint>(),
            cancellationToken).ConfigureAwait(true);
        decision.Status.ShouldBe(HttpStatusCode.OK, decision.Body);
        await AssertNoLicenseAsync(stack, customer, admin, keycloak.StaffFull, rejectedToken, staffToken, keycloak.CustomerB.TenantId, rejected.OrderId, "rejected", cancellationToken).ConfigureAwait(true);

        var unreadyToken = (await keycloak.CustomerTokenAsync(keycloak.CustomerC, cancellationToken).ConfigureAwait(true)).AccessToken;
        var unready = await CommerceFlow.CreatePayableOrderAsync(stack, customer, unreadyToken, CommerceTestConfiguration.SharedOfferId, [], cancellationToken).ConfigureAwait(true);
        stack.Stripe.MarkPaid(unready.OrderId, stack.Stripe.Now.AddMonths(1));
        var unreadyEvent = "evt_unready_" + unready.OrderId.ToString("N");
        await CommerceFlow.WebhookAsync(stack, unreadyEvent, "checkout.session.completed", unready.OrderId, "checkout.session", cancellationToken).ConfigureAwait(true);
        await CommerceFlow.WaitReceiptCompletedAsync(stack, unreadyEvent, cancellationToken).ConfigureAwait(true);
        await CommerceFlow.WaitPaymentAsync(stack, unready.OrderId, cancellationToken).ConfigureAwait(true);
        _ = await CommerceFlow.ApproveAsync(stack, admin, staffToken, keycloak.CustomerC.TenantId, unready.OrderId, "fixture-unready", cancellationToken).ConfigureAwait(true);
        await AssertNoLicenseAsync(stack, customer, admin, keycloak.StaffFull, unreadyToken, staffToken, keycloak.CustomerC.TenantId, unready.OrderId, "pendingReadiness", cancellationToken).ConfigureAwait(true);
    }

    private static async Task<Guid> CreateConcurrentIntentAsync(
        CommerceHttp customer,
        string token,
        JsonObject body,
        Dictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => customer.PostAsync(
            "/v1/commerce/orders",
            token,
            body,
            cancellationToken,
            headers))).ConfigureAwait(true);
        foreach (var response in responses)
        {
            response.Status.ShouldBe(HttpStatusCode.Created, response.Body);
        }

        var orderIds = responses.Select(response => Guid.Parse(response.Json()["order"]!["orderId"]!.GetValue<string>())).Distinct().ToArray();
        orderIds.Length.ShouldBe(1);
        return orderIds.Single();
    }

    private static async Task AssertIdempotencyConflictsAsync(
        CommerceHttp customer,
        string token,
        JsonObject body,
        Dictionary<string, string> headers,
        uint changedRevision,
        CancellationToken cancellationToken)
    {
        var changed = await customer.PostAsync(
            "/v1/commerce/orders",
            token,
            new JsonObject { ["basketRevision"] = changedRevision },
            cancellationToken,
            headers).ConfigureAwait(true);
        changed.Status.ShouldBe(HttpStatusCode.Conflict);
        changed.Code().ShouldBe("idempotency_conflict");

        var stale = await customer.PostAsync(
            "/v1/commerce/orders",
            token,
            body,
            cancellationToken,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Idempotency-Key"] = Guid.NewGuid().ToString("D") }).ConfigureAwait(true);
        stale.Status.ShouldBe(HttpStatusCode.Conflict);
        stale.Code().ShouldBe("stale_revision");
    }

    private static async Task AssertEmptyBasketRejectsOrderAsync(CommerceHttp customer, string token, CancellationToken cancellationToken)
    {
        var current = await customer.GetAsync("/v1/commerce/basket", token, cancellationToken).ConfigureAwait(true);
        current.Status.ShouldBe(HttpStatusCode.OK);
        var revision = current.Json()["revision"]!.GetValue<uint>();
        var cleared = await customer.PutAsync(
            "/v1/commerce/basket",
            token,
            new JsonObject { ["selection"] = null, ["expectedRevision"] = revision },
            cancellationToken).ConfigureAwait(true);
        cleared.Status.ShouldBe(HttpStatusCode.OK);
        cleared.Json()["selection"].ShouldBeNull();
        var emptyRevision = cleared.Json()["revision"]!.GetValue<uint>();
        var empty = await customer.PostAsync(
            "/v1/commerce/orders",
            token,
            new JsonObject { ["basketRevision"] = emptyRevision },
            cancellationToken,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Idempotency-Key"] = Guid.NewGuid().ToString("D") }).ConfigureAwait(true);
        empty.Status.ShouldBe(HttpStatusCode.Conflict);
        empty.Code().ShouldBe("invalid_transition");
    }

    private static async Task AssertNoLicenseAsync(
        CommerceStack stack,
        CommerceHttp customer,
        CommerceHttp admin,
        StaffAccount staffAccount,
        string customerToken,
        string staffToken,
        Guid tenantId,
        Guid orderId,
        string expectedStatus,
        CancellationToken cancellationToken)
    {
        _ = staffAccount;
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(true);
        var projection = await CommerceFlow.CustomerOrderAsync(customer, customerToken, orderId, cancellationToken).ConfigureAwait(true);
        string.Equals((string?)projection["status"], expectedStatus, StringComparison.Ordinal).ShouldBeTrue(projection.ToJsonString());
        projection["license"].ShouldBeNull();
        var adminOrder = await CommerceFlow.AdminOrderAsync(admin, staffToken, tenantId, orderId, cancellationToken).ConfigureAwait(true);
        adminOrder["license"].ShouldBeNull();
        (await Db.ScalarAsync(
            stack.Postgres.Licensing.ConnectionString,
            "SELECT count(*) FROM licensing.license_grants WHERE order_id = $1 AND state = 'active'",
            cancellationToken,
            orderId).ConfigureAwait(true)).ShouldBe(0);
    }
}
