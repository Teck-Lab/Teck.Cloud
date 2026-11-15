using System.Text.Json.Nodes;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Fixture-only users, audiences and permission policies.</summary>
public sealed partial class KeycloakFixture
{
    private async Task ProvisionAsync(CancellationToken cancellationToken)
    {
        using var role = await AdminAsync(HttpMethod.Post, "roles", JsonNode.Parse("""{"name":"records-reader"}"""), cancellationToken).ConfigureAwait(false);
        var definition1 = JsonNode.Parse("""{"clientId":"integration-service","enabled":true,"publicClient":false,"standardFlowEnabled":false,"directAccessGrantsEnabled":false,"serviceAccountsEnabled":true,"authorizationServicesEnabled":true,"authorizationSettings":{"allowRemoteResourceManagement":false,"policyEnforcementMode":"ENFORCING","decisionStrategy":"UNANIMOUS","scopes":[{"name":"read"},{"name":"write"}],"resources":[{"name":"records","scopes":[{"name":"read"},{"name":"write"}]}],"policies":[{"name":"readers","type":"role","logic":"POSITIVE","decisionStrategy":"UNANIMOUS","config":{"roles":"[{\"id\":\"records-reader\",\"required\":true}]"}},{"name":"read-records","type":"scope","logic":"POSITIVE","decisionStrategy":"UNANIMOUS","config":{"resources":"[\"records\"]","scopes":"[\"read\"]","applyPolicies":"[\"readers\"]"}}]}}""");
        using var service = await AdminAsync(HttpMethod.Post, "clients", definition1, cancellationToken).ConfigureAwait(false);
        var definition2 = JsonNode.Parse("""{"clientId":"integration-login","enabled":true,"publicClient":true,"standardFlowEnabled":false,"directAccessGrantsEnabled":true,"defaultClientScopes":["basic","profile","email","roles"],"optionalClientScopes":["organization"],"protocolMappers":[{"name":"gateway","protocol":"openid-connect","protocolMapper":"oidc-audience-mapper","config":{"included.client.audience":"customer-gateway","access.token.claim":"true"}}]}""");
        using var login = await AdminAsync(HttpMethod.Post, "clients", definition2, cancellationToken).ConfigureAwait(false);
        using var gateways = await AdminAsync(HttpMethod.Get, "clients?clientId=customer-gateway", body: null, cancellationToken).ConfigureAwait(false);
        var gatewayId = gateways.RootElement[0].GetProperty("id").GetString();
        var definition3 = JsonNode.Parse("""{"name":"test-service","protocol":"openid-connect","protocolMapper":"oidc-audience-mapper","config":{"included.client.audience":"integration-service","access.token.claim":"true"}}""");
        using var mapper = await AdminAsync(HttpMethod.Post, "clients/" + gatewayId + "/protocol-mappers/models", definition3, cancellationToken).ConfigureAwait(false);

        using var readerRole = await AdminAsync(HttpMethod.Get, "roles/records-reader", body: null, cancellationToken).ConfigureAwait(false);
        using var scopeMapping = await AdminAsync(HttpMethod.Post, "clients/" + gatewayId + "/scope-mappings/realm", new JsonArray(JsonNode.Parse(readerRole.RootElement.GetRawText())), cancellationToken).ConfigureAwait(false);

        // Only this fixture client uses password grants; production web clients retain PKCE and reject them.
        await AddUserAsync("reader", canRead: true, cancellationToken).ConfigureAwait(false);
        await AddUserAsync("denied", canRead: false, cancellationToken).ConfigureAwait(false);
        using var organization = await AdminAsync(HttpMethod.Post, "organizations", JsonNode.Parse("""{"name":"Fixture tenant","alias":"fixture","enabled":true}"""), cancellationToken).ConfigureAwait(false);
        using var organizations = await AdminAsync(HttpMethod.Get, "organizations", body: null, cancellationToken).ConfigureAwait(false);
        OrganizationId = organizations.RootElement[0].GetProperty("id").GetString()!;
        using var readers = await AdminAsync(HttpMethod.Get, "users?username=reader&exact=true", body: null, cancellationToken).ConfigureAwait(false);
        var readerId = readers.RootElement[0].GetProperty("id").GetString();
        using var member = await AdminAsync(HttpMethod.Post, "organizations/" + OrganizationId + "/members", JsonValue.Create(readerId), cancellationToken).ConfigureAwait(false);
    }

    private async Task AddUserAsync(string username, bool canRead, CancellationToken cancellationToken)
    {
        var user = new JsonObject
        {
            ["username"] = username,
            ["email"] = username + "@example.test",
            ["firstName"] = "Integration",
            ["lastName"] = "Fixture",
            ["emailVerified"] = true,
            ["enabled"] = true,
            ["credentials"] = new JsonArray(new JsonObject { ["type"] = "password", ["value"] = _userPassword, ["temporary"] = false }),
        };
        using var created = await AdminAsync(HttpMethod.Post, "users", user, cancellationToken).ConfigureAwait(false);
        if (canRead)
        {
            using var users = await AdminAsync(HttpMethod.Get, "users?username=" + username + "&exact=true", body: null, cancellationToken).ConfigureAwait(false);
            using var role = await AdminAsync(HttpMethod.Get, "roles/records-reader", body: null, cancellationToken).ConfigureAwait(false);
            using var assigned = await AdminAsync(HttpMethod.Post, "users/" + users.RootElement[0].GetProperty("id").GetString() + "/role-mappings/realm", new JsonArray(JsonNode.Parse(role.RootElement.GetRawText())), cancellationToken).ConfigureAwait(false);
        }
    }
}
