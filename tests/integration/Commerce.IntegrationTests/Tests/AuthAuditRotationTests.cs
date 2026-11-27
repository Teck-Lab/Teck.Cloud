using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Commerce.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;
using LicenseV1 = Teck.Cloud.Commerce.Licensing.V1;

namespace Commerce.IntegrationTests.Tests;

/// <summary>Real tenancy denial, staff policy, audit and signing-key rotation.</summary>
/// <param name="keycloak">The shared disposable identity provider.</param>
[Collection(CommerceIntegrationSuite.Name)]
public sealed class AuthAuditRotationTests(CommerceKeycloakFixture keycloak)
{
    /// <summary>Cross-tenant and cross-subject reads are scoped 404 and staff mutations are policy-gated.</summary>
    /// <returns>The authorization assertion task.</returns>
    [Fact]
    public async Task TenancyStaffPoliciesAndAuditAreEnforced()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var stack = await keycloak.StackAsync(cancellationToken).ConfigureAwait(true);
        var tokenA = (await keycloak.CustomerTokenAsync(keycloak.CustomerA, cancellationToken).ConfigureAwait(true)).AccessToken;
        var tokenB = (await keycloak.CustomerTokenAsync(keycloak.CustomerB, cancellationToken).ConfigureAwait(true)).AccessToken;
        var tokenC = (await keycloak.CustomerTokenAsync(keycloak.CustomerC, cancellationToken).ConfigureAwait(true)).AccessToken;
        var staff = await keycloak.StaffTokenAsync(keycloak.StaffFull, cancellationToken).ConfigureAwait(true);
        var readOnly = await keycloak.StaffTokenAsync(keycloak.StaffReadOnly, cancellationToken).ConfigureAwait(true);
        using var customer = new CommerceHttp(stack.CustomerGatewayAddress);
        using var admin = new CommerceHttp(stack.AdminGatewayAddress);

        var orderB = await AssertTenancyAndReadOnlyDenialsAsync(stack, customer, admin, tokenA, tokenB, tokenC, readOnly, cancellationToken).ConfigureAwait(true);
        await AssertRejectionIsAuditedAsync(stack, customer, admin, tokenB, staff, orderB, cancellationToken).ConfigureAwait(true);
        await AssertApprovalIsAuditedAsync(stack, customer, admin, tokenA, staff, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>A signed-but-expired, tampered, wrong-binding or superseded license is rejected.</summary>
    /// <returns>The verifier assertion task.</returns>
    [Fact]
    public async Task LicenseVerificationRejectsTamperedExpiredWrongBindingAndSuperseded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var stack = await keycloak.StackAsync(cancellationToken).ConfigureAwait(true);
        var token = (await keycloak.CustomerTokenAsync(keycloak.CustomerA, cancellationToken).ConfigureAwait(true)).AccessToken;
        var staff = await keycloak.StaffTokenAsync(keycloak.StaffFull, cancellationToken).ConfigureAwait(true);
        using var customer = new CommerceHttp(stack.CustomerGatewayAddress);
        using var admin = new CommerceHttp(stack.AdminGatewayAddress);
        var checkout = await CommerceFlow.ActivateAsync(
            stack,
            customer,
            admin,
            keycloak.CustomerA,
            token,
            staff,
            "shared",
            [],
            cancellationToken).ConfigureAwait(true);
        var active = await CommerceFlow.CustomerOrderAsync(customer, token, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        var document = active["license"]!["document"]!.GetValue<string>();
        var revision = active["license"]!["revision"]!.GetValue<uint>();

        var tampered = await CommerceFlow.VerifyAsync(stack, CommerceFlow.Tamper(document), keycloak.CustomerA.TenantId, checkout.SubscriptionId, cancellationToken).ConfigureAwait(true);
        tampered.Valid.ShouldBeFalse();
        tampered.Rejection.ShouldBe(LicenseV1.RejectionCode.InvalidSignature);

        var expired = CommerceFlow.ExpiredDocument(stack.KeyRing.K1, keycloak.CustomerA.TenantId, checkout.OrderId, checkout.SubscriptionId, "shared", [], revision);
        var expiredResult = await CommerceFlow.VerifyAsync(stack, expired, keycloak.CustomerA.TenantId, checkout.SubscriptionId, cancellationToken).ConfigureAwait(true);
        expiredResult.Valid.ShouldBeFalse();
        expiredResult.Rejection.ShouldBe(LicenseV1.RejectionCode.Expired);

        var wrongTenant = await CommerceFlow.VerifyAsync(stack, document, keycloak.CustomerC.TenantId, checkout.SubscriptionId, cancellationToken).ConfigureAwait(true);
        wrongTenant.Valid.ShouldBeFalse();
        wrongTenant.Rejection.ShouldBe(LicenseV1.RejectionCode.WrongBinding);
        var wrongSubscription = await CommerceFlow.VerifyAsync(stack, document, keycloak.CustomerA.TenantId, Guid.NewGuid(), cancellationToken).ConfigureAwait(true);
        wrongSubscription.Valid.ShouldBeFalse();
        wrongSubscription.Rejection.ShouldBe(LicenseV1.RejectionCode.WrongBinding);

        _ = await CommerceFlow.RecordEvidenceAsync(
            stack,
            admin,
            staff,
            keycloak.CustomerA.TenantId,
            checkout.OrderId,
            checkout.SubscriptionId,
            "shared",
            "supersede-evidence-" + Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(true);
        await stack.RestartOrdersAsync(cancellationToken).ConfigureAwait(true);
        _ = await CommerceFlow.WaitActiveAsync(
            stack,
            customer,
            admin,
            token,
            staff,
            keycloak.CustomerA.TenantId,
            checkout.OrderId,
            cancellationToken).ConfigureAwait(true);
        var superseded = await CommerceFlow.VerifyAsync(stack, document, keycloak.CustomerA.TenantId, checkout.SubscriptionId, cancellationToken).ConfigureAwait(true);
        superseded.Valid.ShouldBeFalse();
        superseded.Rejection.ShouldBe(LicenseV1.RejectionCode.Revoked);
    }

    /// <summary>K1 stays trusted after rotation, K2 signs new grants, and removing K1 is unknown_key.</summary>
    /// <returns>The rotation assertion task.</returns>
    [Fact]
    public async Task RotationKeepsTrustedK1ThenRejectsItAfterRemoval()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var stack = await keycloak.StackAsync(cancellationToken).ConfigureAwait(true);
        var token = (await keycloak.CustomerTokenAsync(keycloak.CustomerA, cancellationToken).ConfigureAwait(true)).AccessToken;
        var staff = await keycloak.StaffTokenAsync(keycloak.StaffFull, cancellationToken).ConfigureAwait(true);
        using var customer = new CommerceHttp(stack.CustomerGatewayAddress);
        using var admin = new CommerceHttp(stack.AdminGatewayAddress);
        var checkout = await CommerceFlow.ActivateAsync(
            stack,
            customer,
            admin,
            keycloak.CustomerA,
            token,
            staff,
            "shared",
            [],
            cancellationToken).ConfigureAwait(true);
        var active = await CommerceFlow.CustomerOrderAsync(customer, token, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        var k1Document = active["license"]!["document"]!.GetValue<string>();
        var k1Revision = active["license"]!["revision"]!.GetValue<uint>();
        active["license"]!["keyId"]!.GetValue<string>().ShouldBe("K1");

        await stack.RotateLicensingToK2Async(cancellationToken).ConfigureAwait(true);
        var k1WhileTrusted = await CommerceFlow.VerifyAsync(stack, k1Document, keycloak.CustomerA.TenantId, checkout.SubscriptionId, cancellationToken).ConfigureAwait(true);
        k1WhileTrusted.Valid.ShouldBeTrue();
        k1WhileTrusted.Claims.KeyId.ShouldBe("K1");

        var rotated = await ReissueUnderK2Async(stack, customer, admin, token, staff, checkout, cancellationToken).ConfigureAwait(true);
        rotated["license"]!["keyId"]!.GetValue<string>().ShouldBe("K2");
        var k2Revision = rotated["license"]!["revision"]!.GetValue<uint>();
        k2Revision.ShouldBeGreaterThan(k1Revision);
        var k2Document = rotated["license"]!["document"]!.GetValue<string>();
        var k2Verified = await CommerceFlow.VerifyAsync(stack, k2Document, keycloak.CustomerA.TenantId, checkout.SubscriptionId, cancellationToken).ConfigureAwait(true);
        k2Verified.Valid.ShouldBeTrue();
        k2Verified.Claims.KeyId.ShouldBe("K2");

        await stack.RemoveK1VerificationKeyAsync(cancellationToken).ConfigureAwait(true);
        var unknown = await CommerceFlow.VerifyAsync(stack, k1Document, keycloak.CustomerA.TenantId, checkout.SubscriptionId, cancellationToken).ConfigureAwait(true);
        unknown.Valid.ShouldBeFalse();
        unknown.Rejection.ShouldBe(LicenseV1.RejectionCode.UnknownKey);
        var stillValid = await CommerceFlow.VerifyAsync(stack, k2Document, keycloak.CustomerA.TenantId, checkout.SubscriptionId, cancellationToken).ConfigureAwait(true);
        stillValid.Valid.ShouldBeTrue();
    }

    /// <summary>The frozen v1 gateway contracts expose no provisioning, deletion or override surface.</summary>
    [Fact]
    public void GatewayContractsExposeNoMutationSurface()
    {
        foreach (var name in new[] { "customer.openapi.yaml", "admin.openapi.yaml" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Contracts", name);
            File.Exists(path).ShouldBeTrue(path);
            var text = File.ReadAllText(path);
            foreach (var forbidden in new[] { "/provision", "/delete", "mark-paid", "force-ready", "readiness-override", "markPaid", "forceReady" })
            {
                text.Contains(forbidden, StringComparison.OrdinalIgnoreCase).ShouldBeFalse(name + " exposes " + forbidden);
            }
        }
    }

    private static async Task<(string ActorId, string Reason, string Action, DateTimeOffset OccurredAt)> AuditAsync(
        CommerceStack stack,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var connection = new Npgsql.NpgsqlConnection(stack.Postgres.Orders.ConnectionString);
        await using var connectionLifetime = connection.ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(true);
        var command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText =
                "SELECT \"ActorId\", \"Reason\", \"Action\", \"OccurredAt\", \"OrderId\"\n" +
                "FROM orders.\"OrderAudit\"\n" +
                "WHERE \"OrderId\" = $1\n" +
                "ORDER BY \"OccurredAt\" DESC\n" +
                "LIMIT 1";
            _ = command.Parameters.AddWithValue(orderId);
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(true);
            await using (reader.ConfigureAwait(false))
            {
                (await reader.ReadAsync(cancellationToken).ConfigureAwait(true)).ShouldBeTrue();
                reader.GetGuid(4).ShouldBe(orderId);
                var action = reader.GetInt32(2) switch
                {
                    1 => "Approve",
                    2 => "Reject",
                    3 => "RecordPlacementEvidence",
                    var other => throw new InvalidOperationException("Unexpected audit action " + other.ToString(CultureInfo.InvariantCulture)),
                };
                return (reader.GetString(0), reader.GetString(1), action, new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc)));
            }
        }
    }

    private async Task<Checkout> AssertTenancyAndReadOnlyDenialsAsync(
        CommerceStack stack,
        CommerceHttp customer,
        CommerceHttp admin,
        string tokenA,
        string tokenB,
        string tokenC,
        string readOnlyToken,
        CancellationToken cancellationToken)
    {
        var orderB = await CommerceFlow.CreatePayableOrderAsync(stack, customer, tokenB, CommerceTestConfiguration.SharedOfferId, [], cancellationToken).ConfigureAwait(true);
        var deniedA = await customer.GetAsync("/v1/commerce/orders/" + orderB.OrderId.ToString("D"), tokenA, cancellationToken).ConfigureAwait(true);
        deniedA.Status.ShouldBe(HttpStatusCode.NotFound);
        deniedA.Code().ShouldBe("not_found");
        var deniedC = await customer.GetAsync("/v1/commerce/orders/" + orderB.OrderId.ToString("D"), tokenC, cancellationToken).ConfigureAwait(true);
        deniedC.Status.ShouldBe(HttpStatusCode.NotFound);
        deniedC.Code().ShouldBe("not_found");

        var customerAtAdmin = await admin.GetAsync(
            "/v1/admin/commerce/tenants/" + keycloak.CustomerB.TenantId.ToString("D") + "/orders",
            tokenA,
            cancellationToken).ConfigureAwait(true);
        customerAtAdmin.Status.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);

        var readOnlyDecision = await CommerceFlow.AdminDecisionAsync(
            admin,
            readOnlyToken,
            keycloak.CustomerB.TenantId,
            orderB.OrderId,
            "reject",
            "read-only-denied",
            1,
            cancellationToken).ConfigureAwait(true);
        readOnlyDecision.Status.ShouldBe(HttpStatusCode.Forbidden);
        var readOnlyEvidence = await CommerceFlow.AdminEvidenceAsync(
            admin,
            readOnlyToken,
            keycloak.CustomerB.TenantId,
            orderB.OrderId,
            CommerceTestConfiguration.SharedPlacementId,
            "read-only-denied",
            1,
            cancellationToken).ConfigureAwait(true);
        readOnlyEvidence.Status.ShouldBe(HttpStatusCode.Forbidden);
        return orderB;
    }

    private async Task AssertRejectionIsAuditedAsync(
        CommerceStack stack,
        CommerceHttp customer,
        CommerceHttp admin,
        string tokenB,
        string staff,
        Checkout orderB,
        CancellationToken cancellationToken)
    {
        stack.Stripe.MarkPaid(orderB.OrderId, stack.Stripe.Now.AddMonths(1));
        var eventB = "evt_audit_b_" + orderB.OrderId.ToString("N");
        await CommerceFlow.WebhookAsync(stack, eventB, "checkout.session.completed", orderB.OrderId, "checkout.session", cancellationToken).ConfigureAwait(true);
        await CommerceFlow.WaitReceiptCompletedAsync(stack, eventB, cancellationToken).ConfigureAwait(true);
        await CommerceFlow.WaitPaymentAsync(stack, orderB.OrderId, cancellationToken).ConfigureAwait(true);
        var pendingB = await CommerceFlow.AdminOrderAsync(admin, staff, keycloak.CustomerB.TenantId, orderB.OrderId, cancellationToken).ConfigureAwait(true);
        var reject = await CommerceFlow.AdminDecisionAsync(
            admin,
            staff,
            keycloak.CustomerB.TenantId,
            orderB.OrderId,
            "reject",
            "fixture-audited-reject",
            pendingB["revision"]!.GetValue<uint>(),
            cancellationToken).ConfigureAwait(true);
        reject.Status.ShouldBe(HttpStatusCode.OK, reject.Body);
        var rejected = await CommerceFlow.CustomerOrderAsync(customer, tokenB, orderB.OrderId, cancellationToken).ConfigureAwait(true);
        string.Equals((string?)rejected["status"], "rejected", StringComparison.Ordinal).ShouldBeTrue();
        rejected["license"].ShouldBeNull();

        var rejectAudit = await AuditAsync(stack, orderB.OrderId, cancellationToken).ConfigureAwait(true);
        rejectAudit.ActorId.ShouldBe(keycloak.StaffFull.Username);
        rejectAudit.Reason.ShouldBe("fixture-audited-reject");
        rejectAudit.Action.ShouldBe("Reject");
        rejectAudit.OccurredAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(5));
        rejectAudit.OccurredAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-10));
    }

    private async Task AssertApprovalIsAuditedAsync(
        CommerceStack stack,
        CommerceHttp customer,
        CommerceHttp admin,
        string tokenA,
        string staff,
        CancellationToken cancellationToken)
    {
        var orderA = await CommerceFlow.CreatePayableOrderAsync(stack, customer, tokenA, CommerceTestConfiguration.SharedOfferId, [], cancellationToken).ConfigureAwait(true);
        stack.Stripe.MarkPaid(orderA.OrderId, stack.Stripe.Now.AddMonths(1));
        var eventA = "evt_audit_a_" + orderA.OrderId.ToString("N");
        await CommerceFlow.WebhookAsync(stack, eventA, "checkout.session.completed", orderA.OrderId, "checkout.session", cancellationToken).ConfigureAwait(true);
        await CommerceFlow.WaitReceiptCompletedAsync(stack, eventA, cancellationToken).ConfigureAwait(true);
        await CommerceFlow.WaitPaymentAsync(stack, orderA.OrderId, cancellationToken).ConfigureAwait(true);
        _ = await CommerceFlow.ApproveAsync(
            stack,
            admin,
            staff,
            keycloak.CustomerA.TenantId,
            orderA.OrderId,
            "fixture-audited-approve",
            cancellationToken).ConfigureAwait(true);
        var approveAudit = await AuditAsync(stack, orderA.OrderId, cancellationToken).ConfigureAwait(true);
        approveAudit.ActorId.ShouldBe(keycloak.StaffFull.Username);
        approveAudit.Reason.ShouldBe("fixture-audited-approve");
        approveAudit.Action.ShouldBe("Approve");
    }

    private async Task<JsonObject> ReissueUnderK2Async(
        CommerceStack stack,
        CommerceHttp customer,
        CommerceHttp admin,
        string token,
        string staff,
        Checkout checkout,
        CancellationToken cancellationToken)
    {
        _ = await CommerceFlow.RecordEvidenceAsync(
            stack,
            admin,
            staff,
            keycloak.CustomerA.TenantId,
            checkout.OrderId,
            checkout.SubscriptionId,
            "shared",
            "rotation-evidence-" + Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(true);
        await stack.RestartOrdersAsync(cancellationToken).ConfigureAwait(true);
        _ = await CommerceFlow.WaitActiveAsync(
            stack,
            customer,
            admin,
            token,
            staff,
            keycloak.CustomerA.TenantId,
            checkout.OrderId,
            cancellationToken).ConfigureAwait(true);
        return await CommerceFlow.CustomerOrderAsync(customer, token, checkout.OrderId, cancellationToken).ConfigureAwait(true);
    }
}
