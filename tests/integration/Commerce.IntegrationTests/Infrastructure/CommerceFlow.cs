using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Grpc.Core;
using Grpc.Net.Client;
using SharedKernel.Infrastructure.Auth;
using Shouldly;
using Standard.Licensing;
using LicenseV1 = Teck.Cloud.Commerce.Licensing.V1;
using LicensingService = Teck.Cloud.Commerce.Licensing.V1.Licensing;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>Real HTTP/gRPC lifecycle operations shared by the commerce integration cases.</summary>
internal static class CommerceFlow
{
    internal const string WebhookPath = "/internal/billing/stripe/v1/webhook";

    internal static async Task<JsonObject> OfferAsync(CommerceHttp customer, string token, string offerId, CancellationToken cancellationToken)
    {
        var response = await customer.GetAsync("/v1/commerce/catalog", token, cancellationToken).ConfigureAwait(false);
        response.Status.ShouldBe(HttpStatusCode.OK);
        return response.Json()["offers"]!.AsArray()
            .Select(offer => offer!.AsObject())
            .Single(offer => string.Equals((string?)offer["offerId"], offerId, StringComparison.Ordinal));
    }

    internal static async Task<BasketState> PutBasketAsync(
        CommerceHttp customer,
        string token,
        string offerId,
        IReadOnlyList<string> addOns,
        CancellationToken cancellationToken)
    {
        var current = await customer.GetAsync("/v1/commerce/basket", token, cancellationToken).ConfigureAwait(false);
        current.Status.ShouldBe(HttpStatusCode.OK);
        var expected = current.Json()["revision"]!.GetValue<uint>();
        var body = new JsonObject
        {
            ["selection"] = new JsonObject
            {
                ["offerId"] = offerId,
                ["addOnCodes"] = new JsonArray(addOns.Select(code => (JsonNode)JsonValue.Create(code)).ToArray()),
            },
            ["expectedRevision"] = expected,
        };
        var response = await customer.PutAsync("/v1/commerce/basket", token, body, cancellationToken).ConfigureAwait(false);
        response.Status.ShouldBe(HttpStatusCode.OK);
        var json = response.Json();
        json["revision"]!.GetValue<uint>().ShouldBe(expected + 1);
        return new BasketState(
            json["revision"]!.GetValue<uint>(),
            json["total"]?["amountMinor"]?.GetValue<long>() ?? 0,
            string.Equals((string?)json["selection"]?["offerId"], offerId, StringComparison.Ordinal));
    }

    internal static async Task<Checkout> CreatePayableOrderAsync(
        CommerceStack stack,
        CommerceHttp customer,
        string token,
        string offerId,
        IReadOnlyList<string> addOns,
        CancellationToken cancellationToken)
    {
        var basket = await PutBasketAsync(customer, token, offerId, addOns, cancellationToken).ConfigureAwait(false);
        basket.Selected.ShouldBeTrue();
        basket.TotalMinor.ShouldBeGreaterThan(0);
        var key = Guid.NewGuid().ToString("D");
        var body = new JsonObject { ["basketRevision"] = basket.Revision };
        JsonNode? response = null;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var created = await customer.PostAsync(
                "/v1/commerce/orders",
                token,
                body,
                cancellationToken,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["Idempotency-Key"] = key }).ConfigureAwait(false);
            created.Status.ShouldBe(HttpStatusCode.Created, created.Body);
            response = created.Json();
            if (response["checkoutUrl"] is not null)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }

        _ = response.ShouldNotBeNull();
        var order = response["order"]!.AsObject();
        var orderId = Guid.Parse(order["orderId"]!.GetValue<string>());
        await BoundedWait.UntilAsync(
            _ => Task.FromResult(TryProviderState(stack, orderId) is not null),
            TimeSpan.FromSeconds(30),
            "The real Stripe adapter did not bind a provider session.",
            cancellationToken).ConfigureAwait(false);
        var provider = stack.Stripe.State(orderId);
        provider.AmountMinor.ShouldBe(basket.TotalMinor);
        return new Checkout(
            orderId,
            Guid.Parse(order["subscriptionId"]!.GetValue<string>()),
            order,
            response["checkoutUrl"]?.GetValue<string>(),
            basket.TotalMinor,
            key,
            basket.Revision);
    }

    internal static async Task<JsonObject> AdminOrderAsync(CommerceHttp admin, string staffToken, Guid tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        var response = await admin.GetAsync(AdminPath(tenantId, orderId), staffToken, cancellationToken).ConfigureAwait(false);
        response.Status.ShouldBe(HttpStatusCode.OK, response.Body);
        return response.Json().AsObject();
    }

    internal static async Task<JsonObject> CustomerOrderAsync(CommerceHttp customer, string token, Guid orderId, CancellationToken cancellationToken)
    {
        var response = await customer.GetAsync("/v1/commerce/orders/" + orderId.ToString("D"), token, cancellationToken).ConfigureAwait(false);
        response.Status.ShouldBe(HttpStatusCode.OK, response.Body);
        return response.Json().AsObject();
    }

    internal static Task<ApiResponse> AdminDecisionAsync(
        CommerceHttp admin,
        string staffToken,
        Guid tenantId,
        Guid orderId,
        string decision,
        string reason,
        uint expectedRevision,
        CancellationToken cancellationToken) =>
        admin.PostAsync(
            AdminPath(tenantId, orderId) + "/decision",
            staffToken,
            new JsonObject
            {
                ["decision"] = decision,
                ["reason"] = reason,
                ["expectedRevision"] = expectedRevision,
            },
            cancellationToken);

    internal static Task<ApiResponse> AdminEvidenceAsync(
        CommerceHttp admin,
        string staffToken,
        Guid tenantId,
        Guid orderId,
        Guid placementId,
        string evidenceReference,
        uint expectedRevision,
        CancellationToken cancellationToken) =>
        admin.PostAsync(
            AdminPath(tenantId, orderId) + "/placement-evidence",
            staffToken,
            new JsonObject
            {
                ["placementId"] = placementId.ToString("D"),
                ["evidenceReference"] = evidenceReference,
                ["expectedRevision"] = expectedRevision,
            },
            cancellationToken);

    internal static async Task WebhookAsync(CommerceStack stack, string eventId, string type, Guid orderId, string objectType, CancellationToken cancellationToken)
    {
        var payload = stack.Stripe.WebhookPayload(eventId, type, orderId, objectType);
        var signature = stack.Stripe.SignatureHeader(payload, DateTimeOffset.UtcNow);
        using var webhook = new CommerceHttp(stack.OrdersWebhookAddress);
        var response = await webhook.PostRawAsync(
            WebhookPath,
            payload,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Stripe-Signature"] = signature },
            cancellationToken).ConfigureAwait(false);
        response.Status.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Accepted, HttpStatusCode.NoContent);
    }

    internal static Task WaitReceiptCompletedAsync(CommerceStack stack, string eventId, CancellationToken cancellationToken) =>
        BoundedWait.UntilAsync(
            async token => await Db.ScalarAsync(
                stack.Postgres.Orders.ConnectionString,
                "SELECT \"Status\" FROM orders.\"ProviderEventReceipts\" WHERE \"AccountId\" = $1 AND \"EventId\" = $2",
                token,
                StripeProviderFixture.AccountId,
                eventId).ConfigureAwait(false) == 3,
            TimeSpan.FromSeconds(60),
            "The signed webhook receipt did not complete.",
            cancellationToken);

    internal static Task WaitPaymentAsync(CommerceStack stack, Guid orderId, CancellationToken cancellationToken) =>
        BoundedWait.UntilAsync(
            async token => await Db.ScalarAsync(
                stack.Postgres.Orders.ConnectionString,
                "SELECT \"PaymentStatus\" FROM orders.\"Orders\" WHERE \"Id\" = $1",
                token,
                orderId).ConfigureAwait(false) == 2,
            TimeSpan.FromSeconds(90),
            "The authoritative paid period was not committed.",
            cancellationToken);

    internal static async Task<JsonObject> ApproveAsync(
        CommerceStack stack,
        CommerceHttp admin,
        string staffToken,
        Guid tenantId,
        Guid orderId,
        string reason,
        CancellationToken cancellationToken)
    {
        var order = await AdminOrderAsync(admin, staffToken, tenantId, orderId, cancellationToken).ConfigureAwait(false);
        var revision = order["revision"]!.GetValue<uint>();
        var decision = await AdminDecisionAsync(admin, staffToken, tenantId, orderId, "approve", reason, revision, cancellationToken).ConfigureAwait(false);
        decision.Status.ShouldBe(HttpStatusCode.OK, decision.Body);
        var approved = decision.Json()["order"]!.AsObject();
        approved["approval"]!["status"]!.GetValue<string>().ShouldBe("approved");
        approved["approval"]!["actorId"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
        approved["approval"]!["reason"]!.GetValue<string>().ShouldBe(reason);
        _ = DateTimeOffset.Parse(approved["approval"]!["occurredAt"]!.GetValue<string>(), CultureInfo.InvariantCulture);
        var audit = await Db.TextAsync(
            stack.Postgres.Orders.ConnectionString,
            "SELECT \"Reason\" FROM orders.\"OrderAudit\" WHERE \"OrderId\" = $1 AND \"Action\" = 1",
            cancellationToken,
            orderId).ConfigureAwait(false);
        audit.ShouldBe(reason);
        return approved;
    }

    internal static async Task<JsonObject> RecordEvidenceAsync(
        CommerceStack stack,
        CommerceHttp admin,
        string staffToken,
        Guid tenantId,
        Guid orderId,
        Guid subscriptionId,
        string tier,
        string evidenceReference,
        CancellationToken cancellationToken)
    {
        var placementId = string.Equals(tier, "dedicated", StringComparison.Ordinal)
            ? CommerceTestConfiguration.DedicatedPlacementId
            : CommerceTestConfiguration.SharedPlacementId;
        var binding = new PlacementBinding(tenantId, orderId, subscriptionId, placementId);
        var target = string.Equals(tier, "dedicated", StringComparison.Ordinal)
            ? stack.Postgres.DedicatedTarget
            : stack.Postgres.SharedTarget;
        await CommercePostgresFixture.BindPlacementAsync(target, binding, cancellationToken).ConfigureAwait(false);
        if (string.Equals(tier, "dedicated", StringComparison.Ordinal))
        {
            await stack.Postgres.WaitForStandbyBindingAsync(binding, cancellationToken).ConfigureAwait(false);
        }

        var order = await AdminOrderAsync(admin, staffToken, tenantId, orderId, cancellationToken).ConfigureAwait(false);
        var revision = order["revision"]!.GetValue<uint>();
        var evidence = await AdminEvidenceAsync(
            admin,
            staffToken,
            tenantId,
            orderId,
            placementId,
            evidenceReference,
            revision,
            cancellationToken).ConfigureAwait(false);
        evidence.Status.ShouldBe(HttpStatusCode.OK, evidence.Body);
        return evidence.Json()["order"]!.AsObject();
    }

    internal static async Task<JsonObject> WaitActiveAsync(
        CommerceStack stack,
        CommerceHttp customer,
        CommerceHttp admin,
        string customerToken,
        string staffToken,
        Guid tenantId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        JsonObject? active = null;
        await BoundedWait.UntilAsync(
            async token =>
            {
                var response = await customer.GetAsync("/v1/commerce/orders/" + orderId.ToString("D"), customerToken, token).ConfigureAwait(false);
                if (response.Status != HttpStatusCode.OK)
                {
                    return false;
                }

                var order = response.Json().AsObject();
                if (!string.Equals((string?)order["status"], "active", StringComparison.Ordinal) || order["license"] is null)
                {
                    return false;
                }

                active = order;
                return true;
            },
            TimeSpan.FromMinutes(3),
            "The order never became active through the real outbox, broker and inbox path.",
            cancellationToken).ConfigureAwait(false);
        _ = active.ShouldNotBeNull();
        var adminOrder = await AdminOrderAsync(admin, staffToken, tenantId, orderId, cancellationToken).ConfigureAwait(false);
        string.Equals((string?)adminOrder["status"], "active", StringComparison.Ordinal).ShouldBeTrue();
        string.Equals((string?)adminOrder["fulfillment"]!["status"], "ready", StringComparison.Ordinal).ShouldBeTrue();
        (adminOrder["license"] is null).ShouldBeTrue();
        var desired = await DesiredRevisionAsync(stack, orderId, cancellationToken).ConfigureAwait(false);
        desired.ShouldBeGreaterThan(0u);
        var applied = await Db.ScalarAsync(
            stack.Postgres.Orders.ConnectionString,
            "SELECT \"AppliedDesiredRevision\" FROM orders.\"Orders\" WHERE \"Id\" = $1",
            cancellationToken,
            orderId).ConfigureAwait(false);
        applied.ShouldBe(desired);
        return active;
    }

    internal static async Task<uint> DesiredRevisionAsync(CommerceStack stack, Guid orderId, CancellationToken cancellationToken)
    {
        var value = await Db.ScalarAsync(
            stack.Postgres.Orders.ConnectionString,
            "SELECT \"DesiredRevision\" FROM orders.\"Orders\" WHERE \"Id\" = $1",
            cancellationToken,
            orderId).ConfigureAwait(false);
        return checked((uint)value);
    }

    internal static async Task<string> WorkloadTokenAsync(CommerceKeycloakFixture keycloak, CancellationToken cancellationToken)
    {
        using var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false };
        using var http = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(20) };
        var client = new OAuthTokenClient(http, new OAuthClientOptions
        {
            TokenEndpoint = keycloak.TokenEndpoint,
            ClientId = keycloak.CustomerGatewayClientId,
            ClientSecret = keycloak.CustomerGatewayClientSecret,
            Transport = IdentityTransport.IsolatedDevelopment,
        });
        var token = await client.ClientCredentialsAsync("commerce-licensing", cancellationToken).ConfigureAwait(false);
        return token.AccessToken;
    }

    internal static GrpcChannel LicensingChannel(CommerceStack stack) => GrpcChannel.ForAddress(
        stack.LicensingAddress,
        new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true },
        });

    internal static Metadata LicensingHeaders(string token, Guid tenantId)
    {
        return new Metadata
        {
            { "authorization", "Bearer " + token },
            { "teck-tenant-id", tenantId.ToString("D") },
        };
    }

    internal static async Task<LicenseV1.VerifyLicenseResponse> VerifyAsync(
        CommerceStack stack,
        string document,
        Guid tenantId,
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        var token = await WorkloadTokenAsync(stack.Keycloak, cancellationToken).ConfigureAwait(false);
        using var channel = LicensingChannel(stack);
        var client = new LicensingService.LicensingClient(channel);
        return await client.VerifyLicenseAsync(
            new LicenseV1.VerifyLicenseRequest
            {
                Document = document,
                ExpectedTenantId = tenantId.ToString("D"),
                ExpectedSubscriptionId = subscriptionId.ToString("D"),
            },
            LicensingHeaders(token, tenantId),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<LicenseV1.GetCurrentLicenseResponse> GetCurrentLicenseAsync(
        CommerceStack stack,
        Guid tenantId,
        Guid orderId,
        Guid subscriptionId,
        uint desiredRevision,
        CancellationToken cancellationToken)
    {
        var token = await WorkloadTokenAsync(stack.Keycloak, cancellationToken).ConfigureAwait(false);
        using var channel = LicensingChannel(stack);
        var client = new LicensingService.LicensingClient(channel);
        return await client.GetCurrentLicenseAsync(
            new LicenseV1.GetCurrentLicenseRequest
            {
                TenantId = tenantId.ToString("D"),
                OrderId = orderId.ToString("D"),
                SubscriptionId = subscriptionId.ToString("D"),
                DesiredRevision = desiredRevision,
            },
            LicensingHeaders(token, tenantId),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<RpcException> GetCurrentLicenseMissAsync(
        CommerceStack stack,
        Guid tenantId,
        Guid orderId,
        Guid subscriptionId,
        uint desiredRevision,
        CancellationToken cancellationToken)
    {
        var token = await WorkloadTokenAsync(stack.Keycloak, cancellationToken).ConfigureAwait(false);
        using var channel = LicensingChannel(stack);
        var client = new LicensingService.LicensingClient(channel);
        return await Should.ThrowAsync<RpcException>(async () => await client.GetCurrentLicenseAsync(
            new LicenseV1.GetCurrentLicenseRequest
            {
                TenantId = tenantId.ToString("D"),
                OrderId = orderId.ToString("D"),
                SubscriptionId = subscriptionId.ToString("D"),
                DesiredRevision = desiredRevision,
            },
            LicensingHeaders(token, tenantId),
            cancellationToken: cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
    }

    internal static string ExpiredDocument(CommerceKey key, Guid tenantId, Guid orderId, Guid subscriptionId, string tier, IReadOnlyList<string> addOns, uint revision)
    {
        var expiresAt = new DateTime(DateTime.UtcNow.AddMinutes(-5).Ticks, DateTimeKind.Utc);
        return License.New()
            .WithUniqueIdentifier(Guid.NewGuid())
            .As(LicenseType.Standard)
            .ExpiresAt(expiresAt)
            .WithAdditionalAttributes(attributes =>
            {
                attributes.Add("tenant_id", tenantId.ToString("D"));
                attributes.Add("order_id", orderId.ToString("D"));
                attributes.Add("subscription_id", subscriptionId.ToString("D"));
                attributes.Add("tier", tier);
                attributes.Add("add_on_codes", string.Join(',', addOns));
                attributes.Add("revision", revision.ToString(CultureInfo.InvariantCulture));
                attributes.Add("key_id", key.KeyId);
                attributes.Add("expires_at", new DateTimeOffset(expiresAt, TimeSpan.Zero).ToString("O", CultureInfo.InvariantCulture));
            })
            .CreateAndSignWithPrivateKey(key.PrivateKey, key.PassPhrase)
            .ToString();
    }

    internal static string Tamper(string document)
    {
        var start = document.IndexOf("<Attribute name=\"tenant_id\">", StringComparison.Ordinal);
        start.ShouldBeGreaterThan(-1);
        var valueStart = start + "<Attribute name=\"tenant_id\">".Length;
        var valueEnd = document.IndexOf('<', valueStart);
        var replacement = Guid.NewGuid().ToString("D");
        return document[..valueStart] + replacement + document[valueEnd..];
    }

    internal static string AdminPath(Guid tenantId, Guid orderId) =>
        "/v1/admin/commerce/tenants/" + tenantId.ToString("D") + "/orders/" + orderId.ToString("D");

    internal static async Task<Checkout> ActivateAsync(
        CommerceStack stack,
        CommerceHttp customer,
        CommerceHttp admin,
        CustomerAccount account,
        string customerToken,
        string staffToken,
        string tier,
        IReadOnlyList<string> addOns,
        CancellationToken cancellationToken)
    {
        var offerId = string.Equals(tier, "dedicated", StringComparison.Ordinal)
            ? CommerceTestConfiguration.DedicatedOfferId
            : CommerceTestConfiguration.SharedOfferId;
        var checkout = await CreatePayableOrderAsync(stack, customer, customerToken, offerId, addOns, cancellationToken).ConfigureAwait(false);
        stack.Stripe.MarkPaid(checkout.OrderId, stack.Stripe.Now.AddMonths(1));
        var paidEvent = "evt_active_" + checkout.OrderId.ToString("N");
        await WebhookAsync(stack, paidEvent, "checkout.session.completed", checkout.OrderId, "checkout.session", cancellationToken).ConfigureAwait(false);
        await WaitReceiptCompletedAsync(stack, paidEvent, cancellationToken).ConfigureAwait(false);
        await WaitPaymentAsync(stack, checkout.OrderId, cancellationToken).ConfigureAwait(false);
        _ = await ApproveAsync(stack, admin, staffToken, account.TenantId, checkout.OrderId, "fixture-active-approval", cancellationToken).ConfigureAwait(false);
        _ = await RecordEvidenceAsync(
            stack,
            admin,
            staffToken,
            account.TenantId,
            checkout.OrderId,
            checkout.SubscriptionId,
            tier,
            "fixture-active-evidence-" + checkout.OrderId.ToString("N"),
            cancellationToken).ConfigureAwait(false);
        await stack.RestartOrdersAsync(cancellationToken).ConfigureAwait(false);
        _ = await WaitActiveAsync(
            stack,
            customer,
            admin,
            customerToken,
            staffToken,
            account.TenantId,
            checkout.OrderId,
            cancellationToken).ConfigureAwait(false);
        return checkout;
    }

    private static ProviderState? TryProviderState(CommerceStack stack, Guid orderId)
    {
        try
        {
            return stack.Stripe.State(orderId);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }
}
