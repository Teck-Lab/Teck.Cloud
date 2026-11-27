using System.Collections.Concurrent;
using System.Net;
using System.Text.Json.Nodes;
using Commerce.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace Commerce.IntegrationTests.Tests;

/// <summary>Real opaque cursor isolation across tenants and subjects.</summary>
/// <param name="keycloak">The shared disposable identity provider.</param>
[Collection(CommerceIntegrationSuite.Name)]
public sealed class CursorIsolationTests(CommerceKeycloakFixture keycloak)
{
    private static readonly SemaphoreSlim SeedGate = new(1, 1);
    private static CursorSeed? _seed;

    /// <summary>Customer C's tenant-two cursor cannot read tenant one as customer A.</summary>
    /// <returns>The isolation assertion task.</returns>
    [Fact]
    public async Task CustomerCursorCannotCrossTenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken).ConfigureAwait(true);
        await seed.RefreshAsync(keycloak, cancellationToken).ConfigureAwait(true);
        var source = await PageAsync(seed.Stack, seed.CustomerHttp, "/v1/commerce/orders?limit=1", seed.CustomerC, cancellationToken).ConfigureAwait(true);
        source.Items.Length.ShouldBe(1);
        _ = source.NextCursor.ShouldNotBeNull();
        _ = source.Items.Single()["license"].ShouldNotBeNull();
        var sourceSecond = await FollowAsync(seed.Stack, seed.CustomerHttp, "/v1/commerce/orders", seed.CustomerC, source.NextCursor, cancellationToken).ConfigureAwait(true);
        sourceSecond.Items.Length.ShouldBe(1);
        _ = sourceSecond.Items[0]["license"].ShouldNotBeNull();
        seed.CustomerOrders[seed.AccountC].Contains(Guid.Parse(sourceSecond.Items[0]["orderId"]!.GetValue<string>())).ShouldBeTrue();

        await AssertPositiveControlAsync(seed.CustomerHttp, "/v1/commerce/orders", seed.CustomerA, seed.CustomerOrders[seed.AccountA], cancellationToken).ConfigureAwait(true);
        await AssertForeignCursorAsync(
            seed.Stack,
            seed.CustomerHttp,
            "/v1/commerce/orders",
            seed.CustomerA,
            source.NextCursor,
            seed.CustomerOrders[seed.AccountA],
            seed.CustomerSubscriptions[seed.AccountA],
            seed.CustomerSubscriptions[seed.AccountC],
            seed.CustomerOrders[seed.AccountC],
            cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Customer B's tenant-one cursor cannot read customer A's subject scope.</summary>
    /// <returns>The isolation assertion task.</returns>
    [Fact]
    public async Task CustomerCursorCannotCrossSubject()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken).ConfigureAwait(true);
        await seed.RefreshAsync(keycloak, cancellationToken).ConfigureAwait(true);
        var source = await PageAsync(seed.Stack, seed.CustomerHttp, "/v1/commerce/orders?limit=1", seed.CustomerB, cancellationToken).ConfigureAwait(true);
        source.Items.Length.ShouldBe(1);
        _ = source.NextCursor.ShouldNotBeNull();
        _ = source.Items.Single()["license"].ShouldNotBeNull();

        await AssertPositiveControlAsync(seed.CustomerHttp, "/v1/commerce/orders", seed.CustomerA, seed.CustomerOrders[seed.AccountA], cancellationToken).ConfigureAwait(true);
        await AssertForeignCursorAsync(
            seed.Stack,
            seed.CustomerHttp,
            "/v1/commerce/orders",
            seed.CustomerA,
            source.NextCursor,
            seed.CustomerOrders[seed.AccountA],
            seed.CustomerSubscriptions[seed.AccountA],
            seed.CustomerSubscriptions[seed.AccountB],
            seed.CustomerOrders[seed.AccountB],
            cancellationToken).ConfigureAwait(true);
    }

    /// <summary>A tenant-two admin cursor cannot list tenant one.</summary>
    /// <returns>The isolation assertion task.</returns>
    [Fact]
    public async Task AdminCursorCannotCrossTenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken).ConfigureAwait(true);
        await seed.RefreshAsync(keycloak, cancellationToken).ConfigureAwait(true);
        var sourcePath = "/v1/admin/commerce/tenants/" + keycloak.TenantTwoId.ToString("D") + "/orders?limit=1";
        var source = await PageAsync(seed.Stack, seed.AdminHttp, sourcePath, seed.StaffToken, cancellationToken).ConfigureAwait(true);
        source.Items.Length.ShouldBe(1);
        _ = source.NextCursor.ShouldNotBeNull();
        var secondPath = "/v1/admin/commerce/tenants/" + keycloak.TenantTwoId.ToString("D") + "/orders?limit=1&cursor=" + Uri.EscapeDataString(source.NextCursor);
        var second = await PageAsync(seed.Stack, seed.AdminHttp, secondPath, seed.StaffToken, cancellationToken).ConfigureAwait(true);
        second.Items.Any(item => seed.TenantOrders[keycloak.TenantTwoId].Contains(Guid.Parse(item["orderId"]!.GetValue<string>()))).ShouldBeTrue();

        var targetPath = "/v1/admin/commerce/tenants/" + keycloak.TenantOneId.ToString("D") + "/orders";
        await AssertPositiveControlAsync(seed.AdminHttp, targetPath, seed.StaffToken, seed.TenantOrders[keycloak.TenantOneId], cancellationToken).ConfigureAwait(true);
        await AssertForeignCursorAsync(
            seed.Stack,
            seed.AdminHttp,
            targetPath,
            seed.StaffToken,
            source.NextCursor,
            seed.TenantOrders[keycloak.TenantOneId],
            seed.TenantSubscriptions[keycloak.TenantOneId],
            seed.TenantSubscriptions[keycloak.TenantTwoId],
            seed.TenantOrders[keycloak.TenantTwoId],
            cancellationToken).ConfigureAwait(true);
    }

    private static async Task AssertPositiveControlAsync(
        CommerceHttp client,
        string route,
        string token,
        HashSet<Guid> allowedOrders,
        CancellationToken cancellationToken)
    {
        var response = await client.GetAsync(route, token, cancellationToken).ConfigureAwait(true);
        response.Status.ShouldBe(HttpStatusCode.OK, response.Body);
        var items = response.Json()["items"]!.AsArray();
        items.Count.ShouldBeGreaterThan(0);
        items.Any(item => allowedOrders.Contains(Guid.Parse(item!["orderId"]!.GetValue<string>()))).ShouldBeTrue();
    }

    private static async Task AssertForeignCursorAsync(
        CommerceStack stack,
        CommerceHttp client,
        string route,
        string token,
        string cursor,
        HashSet<Guid> allowedOrders,
        HashSet<Guid> allowedSubscriptions,
        HashSet<Guid> foreignSubscriptions,
        HashSet<Guid> foreignOrders,
        CancellationToken cancellationToken)
    {
        var path = route + "?limit=1&cursor=" + Uri.EscapeDataString(cursor);
        var response = await client.GetAsync(path, token, cancellationToken).ConfigureAwait(true);
        if (response.Status == HttpStatusCode.OK)
        {
            var seen = new HashSet<Guid>();
            var pages = 0;
            while (true)
            {
                pages.ShouldBeLessThanOrEqualTo(allowedOrders.Count + 1);
                var page = response.Json().AsObject();
                foreach (var item in page["items"]!.AsArray())
                {
                    var order = item!.AsObject();
                    var orderId = Guid.Parse(order["orderId"]!.GetValue<string>());
                    var subscriptionId = Guid.Parse(order["subscriptionId"]!.GetValue<string>());
                    allowedOrders.Contains(orderId).ShouldBeTrue("A foreign cursor returned an order outside the independently authorized scope.");
                    allowedSubscriptions.Contains(subscriptionId).ShouldBeTrue("A foreign cursor returned a subscription outside the independently authorized scope.");
                    foreignOrders.Contains(orderId).ShouldBeFalse();
                    foreignSubscriptions.Contains(subscriptionId).ShouldBeFalse();
                    order["license"].ShouldBeNull();
                    seen.Add(orderId);
                }

                if (page["nextCursor"] is null)
                {
                    break;
                }

                var next = page["nextCursor"]!.GetValue<string>();
                seen.Count.ShouldBeLessThan(allowedOrders.Count + 1);
                response = await client.GetAsync(route + "?limit=1&cursor=" + Uri.EscapeDataString(next), token, cancellationToken).ConfigureAwait(true);
                response.Status.ShouldBe(HttpStatusCode.OK, response.Body);
                pages++;
            }

            return;
        }

        response.Status.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
        response.IsProblem().ShouldBeTrue(response.Body);
        var code = response.Code();
        code.ShouldBeOneOf("invalid_request", "not_found", "idempotency_conflict", "stale_revision", "invalid_transition", "unsupported_option");
        foreach (var foreign in foreignOrders)
        {
            response.Body.ShouldNotContain(foreign.ToString("D"), Case.Sensitive);
        }

        foreach (var foreign in foreignSubscriptions)
        {
            response.Body.ShouldNotContain(foreign.ToString("D"), Case.Sensitive);
        }

        response.Body.ShouldNotContain("fixture-license", Case.Sensitive);
        _ = stack;
    }

    private static async Task<Page> PageAsync(CommerceStack stack, CommerceHttp client, string path, string token, CancellationToken cancellationToken)
    {
        _ = stack;
        var response = await client.GetAsync(path, token, cancellationToken).ConfigureAwait(true);
        response.Status.ShouldBe(HttpStatusCode.OK, response.Body);
        var page = response.Json().AsObject();
        return new Page(
            [.. page["items"]!.AsArray().Select(item => item!.AsObject())],
            page["nextCursor"]?.GetValue<string>());
    }

    private static Task<Page> FollowAsync(CommerceStack stack, CommerceHttp client, string route, string token, string cursor, CancellationToken cancellationToken) =>
        PageAsync(stack, client, route + "?limit=1&cursor=" + Uri.EscapeDataString(cursor), token, cancellationToken);

    private static async Task<HashSet<Guid>> OrderIdsAsync(
        CommerceStack stack,
        Guid tenantId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        var command = new Npgsql.NpgsqlCommand(
            "SELECT \"Id\" FROM orders.\"Orders\" WHERE \"TenantId\" = $1 AND \"SubjectId\" = $2");
        await using var commandLifetime = command.ConfigureAwait(false);
        return await IdsAsync(stack, command, cancellationToken, tenantId.ToString("D"), subjectId).ConfigureAwait(false);
    }

    private static async Task<HashSet<Guid>> SubscriptionIdsAsync(
        CommerceStack stack,
        Guid tenantId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        var command = new Npgsql.NpgsqlCommand(
            "SELECT \"SubscriptionId\" FROM orders.\"Orders\" WHERE \"TenantId\" = $1 AND \"SubjectId\" = $2");
        await using var commandLifetime = command.ConfigureAwait(false);
        return await IdsAsync(stack, command, cancellationToken, tenantId.ToString("D"), subjectId).ConfigureAwait(false);
    }

    private static async Task<HashSet<Guid>> TenantOrderIdsAsync(
        CommerceStack stack,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var command = new Npgsql.NpgsqlCommand(
            "SELECT \"Id\" FROM orders.\"Orders\" WHERE \"TenantId\" = $1");
        await using var commandLifetime = command.ConfigureAwait(false);
        return await IdsAsync(stack, command, cancellationToken, tenantId.ToString("D")).ConfigureAwait(false);
    }

    private static async Task<HashSet<Guid>> TenantSubscriptionIdsAsync(
        CommerceStack stack,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var command = new Npgsql.NpgsqlCommand(
            "SELECT \"SubscriptionId\" FROM orders.\"Orders\" WHERE \"TenantId\" = $1");
        await using var commandLifetime = command.ConfigureAwait(false);
        return await IdsAsync(stack, command, cancellationToken, tenantId.ToString("D")).ConfigureAwait(false);
    }

    private static async Task<HashSet<Guid>> IdsAsync(
        CommerceStack stack,
        Npgsql.NpgsqlCommand command,
        CancellationToken cancellationToken,
        params object?[] parameters)
    {
        var connection = new Npgsql.NpgsqlConnection(stack.Postgres.Orders.ConnectionString);
        await using var connectionLifetime = connection.ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        command.Connection = connection;
        foreach (var parameter in parameters)
        {
            _ = command.Parameters.AddWithValue(parameter ?? DBNull.Value);
        }

        var ids = new HashSet<Guid>();
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerLifetime = reader.ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    private async Task<CursorSeed> SeedAsync(CancellationToken cancellationToken)
    {
        if (_seed is not null)
        {
            return _seed;
        }

        await SeedGate.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            return _seed ??= await CreateSeedAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            _ = SeedGate.Release();
        }
    }

    private async Task<CursorSeed> CreateSeedAsync(CancellationToken cancellationToken)
    {
        var stack = await keycloak.StackAsync(cancellationToken).ConfigureAwait(true);
        var staffToken = await keycloak.StaffTokenAsync(keycloak.StaffFull, cancellationToken).ConfigureAwait(true);
        using var customerHttp = new CommerceHttp(stack.CustomerGatewayAddress);
        using var adminHttp = new CommerceHttp(stack.AdminGatewayAddress);
        await SeedOrdersAsync(stack, customerHttp, adminHttp, staffToken, cancellationToken).ConfigureAwait(true);
        var seed = new CursorSeed
        {
            Stack = stack,
            CustomerHttp = new CommerceHttp(stack.CustomerGatewayAddress),
            AdminHttp = new CommerceHttp(stack.AdminGatewayAddress),
            CustomerA = (await keycloak.CustomerTokenAsync(keycloak.CustomerA, cancellationToken).ConfigureAwait(true)).AccessToken,
            CustomerB = (await keycloak.CustomerTokenAsync(keycloak.CustomerB, cancellationToken).ConfigureAwait(true)).AccessToken,
            CustomerC = (await keycloak.CustomerTokenAsync(keycloak.CustomerC, cancellationToken).ConfigureAwait(true)).AccessToken,
            StaffToken = staffToken,
            AccountA = keycloak.CustomerA,
            AccountB = keycloak.CustomerB,
            AccountC = keycloak.CustomerC,
        };
        await LoadScopesAsync(seed, cancellationToken).ConfigureAwait(true);
        return seed;
    }

    private async Task SeedOrdersAsync(
        CommerceStack stack,
        CommerceHttp customerHttp,
        CommerceHttp adminHttp,
        string staffToken,
        CancellationToken cancellationToken)
    {
        var accounts = new[] { keycloak.CustomerA, keycloak.CustomerB, keycloak.CustomerC };
        var created = new List<(CustomerAccount Account, Checkout Checkout)>();
        foreach (var account in accounts)
        {
            var customerToken = (await keycloak.CustomerTokenAsync(account, cancellationToken).ConfigureAwait(true)).AccessToken;
            for (var index = 0; index < 2; index++)
            {
                var checkout = await CommerceFlow.CreatePayableOrderAsync(
                    stack,
                    customerHttp,
                    customerToken,
                    CommerceTestConfiguration.SharedOfferId,
                    [],
                    cancellationToken).ConfigureAwait(true);
                stack.Stripe.MarkPaid(checkout.OrderId, stack.Stripe.Now.AddMonths(1));
                var eventId = "evt_cursor_paid_" + checkout.OrderId.ToString("N");
                await CommerceFlow.WebhookAsync(stack, eventId, "checkout.session.completed", checkout.OrderId, "checkout.session", cancellationToken).ConfigureAwait(true);
                await CommerceFlow.WaitReceiptCompletedAsync(stack, eventId, cancellationToken).ConfigureAwait(true);
                await CommerceFlow.WaitPaymentAsync(stack, checkout.OrderId, cancellationToken).ConfigureAwait(true);
                _ = await CommerceFlow.ApproveAsync(
                    stack,
                    adminHttp,
                    staffToken,
                    account.TenantId,
                    checkout.OrderId,
                    "cursor-seed-approval",
                    cancellationToken).ConfigureAwait(true);
                _ = await CommerceFlow.RecordEvidenceAsync(
                    stack,
                    adminHttp,
                    staffToken,
                    account.TenantId,
                    checkout.OrderId,
                    checkout.SubscriptionId,
                    "shared",
                    "cursor-seed-evidence-" + checkout.OrderId.ToString("N"),
                    cancellationToken).ConfigureAwait(true);
                created.Add((account, checkout));
            }
        }

        await stack.RestartOrdersAsync(cancellationToken).ConfigureAwait(true);
        await WaitOrdersActiveAsync(stack, customerHttp, adminHttp, created, staffToken, cancellationToken).ConfigureAwait(true);
    }

    private async Task WaitOrdersActiveAsync(
        CommerceStack stack,
        CommerceHttp customerHttp,
        CommerceHttp adminHttp,
        List<(CustomerAccount Account, Checkout Checkout)> created,
        string staffToken,
        CancellationToken cancellationToken)
    {
        foreach (var (account, checkout) in created)
        {
            var token = (await keycloak.CustomerTokenAsync(account, cancellationToken).ConfigureAwait(true)).AccessToken;
            _ = await CommerceFlow.WaitActiveAsync(
                stack,
                customerHttp,
                adminHttp,
                token,
                staffToken,
                account.TenantId,
                checkout.OrderId,
                cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task LoadScopesAsync(CursorSeed seed, CancellationToken cancellationToken)
    {
        seed.CustomerOrders[keycloak.CustomerA] = await OrderIdsAsync(seed.Stack, keycloak.CustomerA.TenantId, keycloak.CustomerA.SubjectId, cancellationToken).ConfigureAwait(true);
        seed.CustomerOrders[keycloak.CustomerB] = await OrderIdsAsync(seed.Stack, keycloak.CustomerB.TenantId, keycloak.CustomerB.SubjectId, cancellationToken).ConfigureAwait(true);
        seed.CustomerOrders[keycloak.CustomerC] = await OrderIdsAsync(seed.Stack, keycloak.CustomerC.TenantId, keycloak.CustomerC.SubjectId, cancellationToken).ConfigureAwait(true);
        seed.CustomerSubscriptions[keycloak.CustomerA] = await SubscriptionIdsAsync(seed.Stack, keycloak.CustomerA.TenantId, keycloak.CustomerA.SubjectId, cancellationToken).ConfigureAwait(true);
        seed.CustomerSubscriptions[keycloak.CustomerB] = await SubscriptionIdsAsync(seed.Stack, keycloak.CustomerB.TenantId, keycloak.CustomerB.SubjectId, cancellationToken).ConfigureAwait(true);
        seed.CustomerSubscriptions[keycloak.CustomerC] = await SubscriptionIdsAsync(seed.Stack, keycloak.CustomerC.TenantId, keycloak.CustomerC.SubjectId, cancellationToken).ConfigureAwait(true);
        seed.TenantOrders[keycloak.TenantOneId] = await TenantOrderIdsAsync(seed.Stack, keycloak.TenantOneId, cancellationToken).ConfigureAwait(true);
        seed.TenantOrders[keycloak.TenantTwoId] = await TenantOrderIdsAsync(seed.Stack, keycloak.TenantTwoId, cancellationToken).ConfigureAwait(true);
        seed.TenantSubscriptions[keycloak.TenantOneId] = await TenantSubscriptionIdsAsync(seed.Stack, keycloak.TenantOneId, cancellationToken).ConfigureAwait(true);
        seed.TenantSubscriptions[keycloak.TenantTwoId] = await TenantSubscriptionIdsAsync(seed.Stack, keycloak.TenantTwoId, cancellationToken).ConfigureAwait(true);
    }

    private sealed class CursorSeed
    {
        internal required CommerceStack Stack { get; init; }

        internal required CommerceHttp CustomerHttp { get; init; }

        internal required CommerceHttp AdminHttp { get; init; }

        internal string CustomerA { get; set; } = string.Empty;

        internal string CustomerB { get; set; } = string.Empty;

        internal string CustomerC { get; set; } = string.Empty;

        internal string StaffToken { get; set; } = string.Empty;

        internal required CustomerAccount AccountA { get; init; }

        internal required CustomerAccount AccountB { get; init; }

        internal required CustomerAccount AccountC { get; init; }

        internal ConcurrentDictionary<CustomerAccount, HashSet<Guid>> CustomerOrders { get; } = new();

        internal ConcurrentDictionary<CustomerAccount, HashSet<Guid>> CustomerSubscriptions { get; } = new();

        internal ConcurrentDictionary<Guid, HashSet<Guid>> TenantOrders { get; } = new();

        internal ConcurrentDictionary<Guid, HashSet<Guid>> TenantSubscriptions { get; } = new();

        internal async Task RefreshAsync(CommerceKeycloakFixture fixture, CancellationToken cancellationToken)
        {
            CustomerA = (await fixture.CustomerTokenAsync(AccountA, cancellationToken).ConfigureAwait(false)).AccessToken;
            CustomerB = (await fixture.CustomerTokenAsync(AccountB, cancellationToken).ConfigureAwait(false)).AccessToken;
            CustomerC = (await fixture.CustomerTokenAsync(AccountC, cancellationToken).ConfigureAwait(false)).AccessToken;
            StaffToken = await fixture.StaffTokenAsync(fixture.StaffFull, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record Page(JsonObject[] Items, string? NextCursor);
}
