using System.Net;
using Commerce.IntegrationTests.Infrastructure;
using Npgsql;
using Shouldly;
using Xunit;

namespace Commerce.IntegrationTests.Tests;

/// <summary>Placement work leases, terminal probes, proof expiry and stalled durable delivery.</summary>
/// <param name="keycloak">The shared disposable identity provider.</param>
[Collection(CommerceIntegrationSuite.Name)]
public sealed class PlacementDurabilityTests(CommerceKeycloakFixture keycloak)
{
    private const string ExpireProofSql =
        "UPDATE orders.\"Orders\" SET \"ProofObservedAt\" = $2 WHERE \"Id\" = $1";

    private const string ResetProbeWorkSql =
        "UPDATE orders.\"PlacementProbeWork\" SET \"DueAt\" = $2, \"LeaseToken\" = NULL, " +
        "\"LeaseUntil\" = NULL, \"Terminal\" = false WHERE \"OrderId\" = $1";

    /// <summary>An expired proof fails closed, then due work recovers without a new approval.</summary>
    /// <returns>The expiry and lease assertion task.</returns>
    [Fact]
    public async Task ExpiredProofFailsClosedAndDueWorkRecoversWithoutReapproval()
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
        var approvalAudits = await AuditCountAsync(stack, checkout.OrderId, 1, cancellationToken).ConfigureAwait(true);
        approvalAudits.ShouldBe(1);

        await ExpireProofAsync(stack, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        var expired = await CommerceFlow.CustomerOrderAsync(customer, token, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        expired["license"].ShouldBeNull();
        string.Equals((string?)expired["status"], "active", StringComparison.Ordinal).ShouldBeFalse();
        var rejected = await CommerceFlow.VerifyAsync(stack, document, keycloak.CustomerA.TenantId, checkout.SubscriptionId, cancellationToken).ConfigureAwait(true);
        rejected.Valid.ShouldBeFalse();

        await ResetProbeWorkAsync(stack, checkout.OrderId, cancellationToken).ConfigureAwait(true);
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
        (await AuditCountAsync(stack, checkout.OrderId, 1, cancellationToken).ConfigureAwait(true)).ShouldBe(1);
        var dueAt = await Db.TimestampAsync(
            stack.Postgres.Orders.ConnectionString,
            "SELECT \"DueAt\" FROM orders.\"PlacementProbeWork\" WHERE \"OrderId\" = $1",
            cancellationToken,
            checkout.OrderId).ConfigureAwait(true);
        _ = dueAt.ShouldNotBeNull();
    }

    /// <summary>A conclusive marker mismatch is terminal and never retried into a grant.</summary>
    /// <returns>The terminal-probe assertion task.</returns>
    [Fact]
    public async Task TerminalBindingMismatchNeverRetriesOrActivates()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var stack = await keycloak.StackAsync(cancellationToken).ConfigureAwait(true);
        var token = (await keycloak.CustomerTokenAsync(keycloak.CustomerB, cancellationToken).ConfigureAwait(true)).AccessToken;
        var staff = await keycloak.StaffTokenAsync(keycloak.StaffFull, cancellationToken).ConfigureAwait(true);
        using var customer = new CommerceHttp(stack.CustomerGatewayAddress);
        using var admin = new CommerceHttp(stack.AdminGatewayAddress);
        var checkout = await CommerceFlow.CreatePayableOrderAsync(stack, customer, token, CommerceTestConfiguration.SharedOfferId, [], cancellationToken).ConfigureAwait(true);
        stack.Stripe.MarkPaid(checkout.OrderId, stack.Stripe.Now.AddMonths(1));
        var paidEvent = "evt_terminal_" + checkout.OrderId.ToString("N");
        await CommerceFlow.WebhookAsync(stack, paidEvent, "checkout.session.completed", checkout.OrderId, "checkout.session", cancellationToken).ConfigureAwait(true);
        await CommerceFlow.WaitReceiptCompletedAsync(stack, paidEvent, cancellationToken).ConfigureAwait(true);
        await CommerceFlow.WaitPaymentAsync(stack, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        _ = await CommerceFlow.ApproveAsync(stack, admin, staff, keycloak.CustomerB.TenantId, checkout.OrderId, "terminal-approval", cancellationToken).ConfigureAwait(true);
        var approved = await CommerceFlow.AdminOrderAsync(admin, staff, keycloak.CustomerB.TenantId, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        var evidence = await CommerceFlow.AdminEvidenceAsync(
            admin,
            staff,
            keycloak.CustomerB.TenantId,
            checkout.OrderId,
            CommerceTestConfiguration.SharedPlacementId,
            "missing-binding-evidence",
            approved["revision"]!.GetValue<uint>(),
            cancellationToken).ConfigureAwait(true);
        evidence.Status.ShouldBe(HttpStatusCode.OK, evidence.Body);
        await stack.RestartOrdersAsync(cancellationToken).ConfigureAwait(true);

        await BoundedWait.UntilAsync(
            async token2 =>
            {
                var order = await CommerceFlow.AdminOrderAsync(admin, staff, keycloak.CustomerB.TenantId, checkout.OrderId, token2).ConfigureAwait(false);
                return string.Equals((string?)order["fulfillment"]!["status"], "failed", StringComparison.Ordinal)
                    && string.Equals((string?)order["fulfillment"]!["failureCode"], "binding_mismatch", StringComparison.Ordinal);
            },
            TimeSpan.FromMinutes(2),
            "A conclusive binding mismatch was not committed as terminal.",
            cancellationToken).ConfigureAwait(true);
        var terminal = await Db.ScalarAsync(
            stack.Postgres.Orders.ConnectionString,
            "SELECT count(*) FROM orders.\"PlacementProbeWork\" WHERE \"OrderId\" = $1 AND \"Terminal\" = true AND \"DueAt\" IS NULL",
            cancellationToken,
            checkout.OrderId).ConfigureAwait(true);
        terminal.ShouldBe(1);
        var projection = await CommerceFlow.CustomerOrderAsync(customer, token, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        projection["license"].ShouldBeNull();
        string.Equals((string?)projection["status"], "failed", StringComparison.Ordinal).ShouldBeTrue();
        (await Db.ScalarAsync(
            stack.Postgres.Licensing.ConnectionString,
            "SELECT count(*) FROM licensing.license_grants WHERE order_id = $1 AND state = 'active'",
            cancellationToken,
            checkout.OrderId).ConfigureAwait(true)).ShouldBe(0);

        await stack.RestartOrdersAsync(cancellationToken).ConfigureAwait(true);
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(true);
        (await Db.ScalarAsync(
            stack.Postgres.Orders.ConnectionString,
            "SELECT count(*) FROM orders.\"PlacementProbeWork\" WHERE \"OrderId\" = $1 AND \"Terminal\" = true",
            cancellationToken,
            checkout.OrderId).ConfigureAwait(true)).ShouldBe(1);
    }

    /// <summary>Withholding Rabbit delivery keeps a fresh proof nonactive until the matching outcome arrives.</summary>
    /// <returns>The stalled-delivery assertion task.</returns>
    [Fact]
    public async Task StalledBrokerDeliveryKeepsCustomerUnreadyUntilRecovery()
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
        var generation = await CommerceFlow.DesiredRevisionAsync(stack, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        var beforeStall = await CommerceFlow.CustomerOrderAsync(customer, token, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        var staleDocument = beforeStall["license"]!["document"]!.GetValue<string>();
        await AssertStalledDeliveryAsync(
            stack,
            customer,
            admin,
            token,
            staff,
            keycloak.CustomerA.TenantId,
            checkout,
            staleDocument,
            cancellationToken).ConfigureAwait(true);

        await stack.RestartLicensingWithKeyRingAsync("K1", stack.KeyRing.Initial(), cancellationToken).ConfigureAwait(true);
        await stack.RestartOrdersAsync(cancellationToken).ConfigureAwait(true);
        var recoveredToken = (await keycloak.CustomerTokenAsync(keycloak.CustomerA, cancellationToken).ConfigureAwait(true)).AccessToken;
        var recoveredStaff = await keycloak.StaffTokenAsync(keycloak.StaffFull, cancellationToken).ConfigureAwait(true);
        _ = await CommerceFlow.WaitActiveAsync(
            stack,
            customer,
            admin,
            recoveredToken,
            recoveredStaff,
            keycloak.CustomerA.TenantId,
            checkout.OrderId,
            cancellationToken).ConfigureAwait(true);

        var nextGeneration = await CommerceFlow.DesiredRevisionAsync(stack, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        nextGeneration.ShouldBeGreaterThan(generation);
        var active = await CommerceFlow.CustomerOrderAsync(customer, recoveredToken, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        _ = active["license"].ShouldNotBeNull();
    }

    private static Task<long> AuditCountAsync(CommerceStack stack, Guid orderId, int action, CancellationToken cancellationToken) =>
        Db.ScalarAsync(
            stack.Postgres.Orders.ConnectionString,
            "SELECT count(*) FROM orders.\"OrderAudit\" WHERE \"OrderId\" = $1 AND \"Action\" = $2",
            cancellationToken,
            orderId,
            action);

    private static async Task AssertStalledDeliveryAsync(
        CommerceStack stack,
        CommerceHttp customer,
        CommerceHttp admin,
        string token,
        string staff,
        Guid tenantId,
        Checkout checkout,
        string staleDocument,
        CancellationToken cancellationToken)
    {
        await stack.Rabbit.StopAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            _ = await CommerceFlow.RecordEvidenceAsync(
                stack,
                admin,
                staff,
                tenantId,
                checkout.OrderId,
                checkout.SubscriptionId,
                "shared",
                "stalled-delivery-evidence-" + Guid.NewGuid().ToString("N"),
                cancellationToken).ConfigureAwait(true);
            await BoundedWait.UntilAsync(
                async token2 =>
                {
                    var observed = await Db.TimestampAsync(
                        stack.Postgres.Orders.ConnectionString,
                        "SELECT \"ProofObservedAt\" FROM orders.\"Orders\" WHERE \"Id\" = $1",
                        token2,
                        checkout.OrderId).ConfigureAwait(false);
                    return observed is not null;
                },
                TimeSpan.FromMinutes(2),
                "The real read-only probe did not complete while the broker was stopped.",
                cancellationToken).ConfigureAwait(true);
            var pending = await CommerceFlow.CustomerOrderAsync(customer, token, checkout.OrderId, cancellationToken).ConfigureAwait(true);
            pending["license"].ShouldBeNull();
            string.Equals((string?)pending["status"], "pendingReadiness", StringComparison.Ordinal).ShouldBeTrue();
            var adminOrder = await CommerceFlow.AdminOrderAsync(admin, staff, tenantId, checkout.OrderId, cancellationToken).ConfigureAwait(true);
            string.Equals((string?)adminOrder["fulfillment"]!["status"], "ready", StringComparison.Ordinal).ShouldBeTrue();
            var stale = await CommerceFlow.VerifyAsync(stack, staleDocument, tenantId, checkout.SubscriptionId, cancellationToken).ConfigureAwait(true);
            stale.Valid.ShouldBeFalse();
            stale.Rejection.ShouldBe(Teck.Cloud.Commerce.Licensing.V1.RejectionCode.Revoked);
        }
        finally
        {
            await stack.Rabbit.StartAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    private static async Task ExpireProofAsync(CommerceStack stack, Guid orderId, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(stack.Postgres.Orders.ConnectionString);
        await using var connectionLifetime = connection.ConfigureAwait(true);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(true);
        var command = connection.CreateCommand();
        await using var commandLifetime = command.ConfigureAwait(true);
        command.CommandText = ExpireProofSql;
        _ = command.Parameters.AddWithValue(orderId);
        _ = command.Parameters.AddWithValue(DateTimeOffset.UtcNow.AddMinutes(-20));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(true);
    }

    private static async Task ResetProbeWorkAsync(CommerceStack stack, Guid orderId, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(stack.Postgres.Orders.ConnectionString);
        await using var connectionLifetime = connection.ConfigureAwait(true);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(true);
        var command = connection.CreateCommand();
        await using var commandLifetime = command.ConfigureAwait(true);
        command.CommandText = ResetProbeWorkSql;
        _ = command.Parameters.AddWithValue(orderId);
        _ = command.Parameters.AddWithValue(DateTimeOffset.UtcNow.AddMinutes(-1));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(true);
    }
}
