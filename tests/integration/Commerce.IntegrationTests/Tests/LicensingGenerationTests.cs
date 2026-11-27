using Commerce.IntegrationTests.Infrastructure;
using Grpc.Core;
using Shouldly;
using Xunit;
using LicenseV1 = Teck.Cloud.Commerce.Licensing.V1;

namespace Commerce.IntegrationTests.Tests;

/// <summary>Durable desired-generation fencing and license revision semantics.</summary>
/// <param name="keycloak">The shared disposable identity provider.</param>
[Collection(CommerceIntegrationSuite.Name)]
public sealed class LicensingGenerationTests(CommerceKeycloakFixture keycloak)
{
    /// <summary>Superseding the same binding makes the old generation an exact miss and one active grant.</summary>
    /// <returns>The generation assertion task.</returns>
    [Fact]
    public async Task SupersededGenerationIsScopedMissAndOneActiveGrantRemains()
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
        var generation = await CommerceFlow.DesiredRevisionAsync(stack, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        generation.ShouldBeGreaterThan(0u);
        var (firstDocument, _) = await ReadFirstGenerationAsync(stack, checkout, generation, cancellationToken).ConfigureAwait(true);
        var nextGeneration = await SupersedeGenerationAsync(stack, customer, admin, customerToken, staffToken, checkout, generation, cancellationToken).ConfigureAwait(true);
        await AssertRemainingActiveGrantAsync(stack, checkout, nextGeneration, firstDocument, cancellationToken).ConfigureAwait(true);
        await AssertWrongBindingIsScopedMissAsync(stack, checkout, nextGeneration, cancellationToken).ConfigureAwait(true);
    }

    private static async Task<string[]> GrantStatesAsync(CommerceStack stack, Guid orderId, CancellationToken cancellationToken)
    {
        var connection = new Npgsql.NpgsqlConnection(stack.Postgres.Licensing.ConnectionString);
        await using var connectionLifetime = connection.ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(true);
        var command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText = "SELECT state FROM licensing.license_grants WHERE order_id = $1 AND is_deleted = false ORDER BY desired_revision";
            _ = command.Parameters.AddWithValue(orderId);
            var states = new List<string>();
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(true);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(true))
                {
                    states.Add(reader.GetString(0));
                }

                return [.. states];
            }
        }
    }

    private async Task<(string Document, uint Revision)> ReadFirstGenerationAsync(
        CommerceStack stack,
        Checkout checkout,
        uint generation,
        CancellationToken cancellationToken)
    {
        var first = await CommerceFlow.GetCurrentLicenseAsync(
            stack,
            keycloak.CustomerA.TenantId,
            checkout.OrderId,
            checkout.SubscriptionId,
            generation,
            cancellationToken).ConfigureAwait(true);
        first.License.Document.ShouldNotBeNullOrWhiteSpace();
        var firstRevision = first.License.Revision;
        firstRevision.ShouldBeGreaterThan(0u);
        return (first.License.Document, firstRevision);
    }

    private async Task<uint> SupersedeGenerationAsync(
        CommerceStack stack,
        CommerceHttp customer,
        CommerceHttp admin,
        string customerToken,
        string staffToken,
        Checkout checkout,
        uint generation,
        CancellationToken cancellationToken)
    {
        _ = await CommerceFlow.RecordEvidenceAsync(
            stack,
            admin,
            staffToken,
            keycloak.CustomerA.TenantId,
            checkout.OrderId,
            checkout.SubscriptionId,
            "shared",
            "generation-two-evidence-" + Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(true);
        await stack.RestartOrdersAsync(cancellationToken).ConfigureAwait(true);
        _ = await CommerceFlow.WaitActiveAsync(
            stack,
            customer,
            admin,
            customerToken,
            staffToken,
            keycloak.CustomerA.TenantId,
            checkout.OrderId,
            cancellationToken).ConfigureAwait(true);
        var nextGeneration = await CommerceFlow.DesiredRevisionAsync(stack, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        nextGeneration.ShouldBeGreaterThan(generation);

        var miss = await CommerceFlow.GetCurrentLicenseMissAsync(
            stack,
            keycloak.CustomerA.TenantId,
            checkout.OrderId,
            checkout.SubscriptionId,
            generation,
            cancellationToken).ConfigureAwait(true);
        miss.StatusCode.ShouldBe(StatusCode.NotFound);
        var trailers = miss.Trailers.Where(entry => string.Equals(entry.Key, "teck-commerce-error-code", StringComparison.Ordinal)).ToArray();
        trailers.Length.ShouldBe(1);
        trailers[0].Value.ShouldBe("not_found");
        return nextGeneration;
    }

    private async Task AssertRemainingActiveGrantAsync(
        CommerceStack stack,
        Checkout checkout,
        uint nextGeneration,
        string firstDocument,
        CancellationToken cancellationToken)
    {
        var current = await CommerceFlow.GetCurrentLicenseAsync(
            stack,
            keycloak.CustomerA.TenantId,
            checkout.OrderId,
            checkout.SubscriptionId,
            nextGeneration,
            cancellationToken).ConfigureAwait(true);
        current.License.Document.ShouldNotBeNullOrWhiteSpace();
        var activeRevision = await Db.ScalarAsync(
            stack.Postgres.Licensing.ConnectionString,
            "SELECT license_revision FROM licensing.license_grants WHERE order_id = $1 AND state = 'active' AND is_deleted = false",
            cancellationToken,
            checkout.OrderId).ConfigureAwait(true);
        current.License.Revision.ShouldBe(checked((uint)activeRevision));
        current.License.Revision.ShouldNotBe(nextGeneration);

        var verified = await CommerceFlow.VerifyAsync(stack, current.License.Document, keycloak.CustomerA.TenantId, checkout.SubscriptionId, cancellationToken).ConfigureAwait(true);
        verified.Valid.ShouldBeTrue();
        verified.Claims.Revision.ShouldBe(current.License.Revision);
        verified.Claims.Tier.ToString().ShouldBe("Shared");

        var states = await GrantStatesAsync(stack, checkout.OrderId, cancellationToken).ConfigureAwait(true);
        states.ShouldContain("superseded", StringComparer.Ordinal);
        states.Count(state => string.Equals(state, "active", StringComparison.Ordinal)).ShouldBe(1);
        (await Db.ScalarAsync(
            stack.Postgres.Licensing.ConnectionString,
            "SELECT count(*) FROM licensing.license_grants WHERE order_id = $1 AND desired_revision = $2 AND state = 'active'",
            cancellationToken,
            checkout.OrderId,
            nextGeneration).ConfigureAwait(true)).ShouldBe(1);

        var oldVerification = await CommerceFlow.VerifyAsync(stack, firstDocument, keycloak.CustomerA.TenantId, checkout.SubscriptionId, cancellationToken).ConfigureAwait(true);
        oldVerification.Valid.ShouldBeFalse();
        oldVerification.Rejection.ShouldBe(LicenseV1.RejectionCode.Revoked);
    }

    private async Task AssertWrongBindingIsScopedMissAsync(
        CommerceStack stack,
        Checkout checkout,
        uint nextGeneration,
        CancellationToken cancellationToken)
    {
        var wrongBinding = await CommerceFlow.GetCurrentLicenseMissAsync(
            stack,
            keycloak.CustomerA.TenantId,
            Guid.NewGuid(),
            checkout.SubscriptionId,
            nextGeneration,
            cancellationToken).ConfigureAwait(true);
        wrongBinding.StatusCode.ShouldBe(StatusCode.NotFound);
        wrongBinding.Trailers.Count(entry => string.Equals(entry.Key, "teck-commerce-error-code", StringComparison.Ordinal)).ShouldBe(1);

        var wrongTenantToken = await CommerceFlow.WorkloadTokenAsync(keycloak, cancellationToken).ConfigureAwait(true);
        using var channel = CommerceFlow.LicensingChannel(stack);
        var client = new LicenseV1.Licensing.LicensingClient(channel);
        var denied = await Should.ThrowAsync<RpcException>(async () => await client.GetCurrentLicenseAsync(
            new LicenseV1.GetCurrentLicenseRequest
            {
                TenantId = keycloak.CustomerA.TenantId.ToString("D"),
                OrderId = checkout.OrderId.ToString("D"),
                SubscriptionId = checkout.SubscriptionId.ToString("D"),
                DesiredRevision = nextGeneration,
            },
            CommerceFlow.LicensingHeaders(wrongTenantToken, keycloak.CustomerC.TenantId),
            cancellationToken: cancellationToken).ConfigureAwait(true)).ConfigureAwait(true);
        denied.StatusCode.ShouldBeOneOf(StatusCode.PermissionDenied, StatusCode.NotFound);
    }
}
