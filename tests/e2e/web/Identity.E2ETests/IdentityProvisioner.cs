using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Identity.E2ETests;

internal sealed class IdentityProvisioner(Uri server) : IDisposable
{
    private readonly HttpClient _http = new(new SocketsHttpHandler { AllowAutoRedirect = false }) { BaseAddress = server, Timeout = TimeSpan.FromSeconds(20) };

    internal string UserPassword { get; } = RandomNumberGenerator.GetHexString(48);

    internal string AlphaId { get; private set; } = string.Empty;

    internal string BetaId { get; private set; } = string.Empty;

    public void Dispose() => _http.Dispose();

    internal async Task InitializeAsync(string username, string password, string secret, Uri gateway, Uri adminGateway, CancellationToken cancellationToken)
    {
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "realms/master/protocol/openid-connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "password",
                ["client_id"] = "admin-cli",
                ["username"] = username,
                ["password"] = password,
            }),
        };
        using var response = await _http.SendAsync(tokenRequest, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        using var grant = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", grant.RootElement.GetProperty("access_token").GetString());
        using var role = await SendAsync(HttpMethod.Post, "roles", JsonNode.Parse("""{"name":"fixture-records-writer"}"""), cancellationToken).ConfigureAwait(false);
        using var adminRole = await SendAsync(HttpMethod.Post, "roles", new JsonObject { ["name"] = "platform-admin" }, cancellationToken).ConfigureAwait(false);
        using var service = await SendAsync(HttpMethod.Post, "clients", JsonNode.Parse("""{"clientId":"identity-service","enabled":true,"publicClient":false,"standardFlowEnabled":false,"directAccessGrantsEnabled":false,"serviceAccountsEnabled":true,"authorizationServicesEnabled":true,"authorizationSettings":{"allowRemoteResourceManagement":false,"policyEnforcementMode":"ENFORCING","decisionStrategy":"UNANIMOUS","scopes":[{"name":"read"},{"name":"write"}],"resources":[{"name":"records","scopes":[{"name":"read"},{"name":"write"}]}],"policies":[{"name":"writers","type":"role","logic":"POSITIVE","decisionStrategy":"UNANIMOUS","config":{"roles":"[{\"id\":\"fixture-records-writer\",\"required\":true}]"}},{"name":"record-permission","type":"scope","logic":"POSITIVE","decisionStrategy":"UNANIMOUS","config":{"resources":"[\"records\"]","scopes":"[\"read\",\"write\"]","applyPolicies":"[\"writers\"]"}}]}}"""), cancellationToken).ConfigureAwait(false);
        var definition = new JsonObject
        {
            ["clientId"] = "identity-gateway",
            ["enabled"] = true,
            ["publicClient"] = false,
            ["secret"] = secret,
            ["standardFlowEnabled"] = true,
            ["directAccessGrantsEnabled"] = false,
            ["serviceAccountsEnabled"] = false,
            ["fullScopeAllowed"] = false,
            ["redirectUris"] = new JsonArray(new Uri(gateway, "/signin-oidc").ToString()),
            ["attributes"] = new JsonObject { ["pkce.code.challenge.method"] = "S256", ["standard.token.exchange.enabled"] = "true", ["post.logout.redirect.uris"] = new Uri(gateway, "/signout-callback-oidc").ToString() },
            ["defaultClientScopes"] = new JsonArray("basic", "roles", "profile", "email", "acr"),
            ["optionalClientScopes"] = new JsonArray("organization"),
            ["protocolMappers"] = new JsonArray(
                JsonNode.Parse("""{"name":"service","protocol":"openid-connect","protocolMapper":"oidc-audience-mapper","config":{"included.client.audience":"identity-service","access.token.claim":"true"}}"""),
                JsonNode.Parse("""{"name":"browser-realm-roles","protocol":"openid-connect","protocolMapper":"oidc-usermodel-realm-role-mapper","config":{"claim.name":"realm_access.roles","jsonType.label":"String","multivalued":"true","id.token.claim":"true","access.token.claim":"false","userinfo.token.claim":"false"}}""")),
        };
        await AddEnrollmentAsync(gateway, cancellationToken).ConfigureAwait(false);
        using var createdGateway = await SendAsync(HttpMethod.Post, "clients", definition, cancellationToken).ConfigureAwait(false);
        using var gateways = await SendAsync(HttpMethod.Get, "clients?clientId=identity-gateway", body: null, cancellationToken).ConfigureAwait(false);
        using var writer = await SendAsync(HttpMethod.Get, "roles/fixture-records-writer", body: null, cancellationToken).ConfigureAwait(false);
        using var scope = await SendAsync(HttpMethod.Post, "clients/" + gateways.RootElement[0].GetProperty("id").GetString() + "/scope-mappings/realm", new JsonArray(JsonNode.Parse(writer.RootElement.GetRawText())), cancellationToken).ConfigureAwait(false);
        var adminDefinition = definition.DeepClone().AsObject();
        adminDefinition["clientId"] = "identity-admin-gateway";
        adminDefinition["optionalClientScopes"] = new JsonArray();
        adminDefinition["redirectUris"] = new JsonArray(new Uri(adminGateway, "/signin-oidc").ToString());
        adminDefinition["authenticationFlowBindingOverrides"] = new JsonObject { ["browser"] = "teck-admin-step-up" };
        adminDefinition["attributes"]!.AsObject()["acr.loa.map"] = "{\"1\":1,\"2\":2}";
        adminDefinition["attributes"]!.AsObject()["minimum.acr.value"] = "2";
        adminDefinition["attributes"]!.AsObject()["access.token.lifespan"] = "60";
        adminDefinition["attributes"]!.AsObject()["post.logout.redirect.uris"] = new Uri(adminGateway, "/signout-callback-oidc").ToString();
        using var createdAdmin = await SendAsync(HttpMethod.Post, "clients", adminDefinition, cancellationToken).ConfigureAwait(false);
        using var adminClients = await SendAsync(HttpMethod.Get, "clients?clientId=identity-admin-gateway", body: null, cancellationToken).ConfigureAwait(false);
        using var platformRole = await SendAsync(HttpMethod.Get, "roles/platform-admin", body: null, cancellationToken).ConfigureAwait(false);
        using var adminScope = await SendAsync(HttpMethod.Post, "clients/" + adminClients.RootElement[0].GetProperty("id").GetString() + "/scope-mappings/realm", new JsonArray(JsonNode.Parse(writer.RootElement.GetRawText()), JsonNode.Parse(platformRole.RootElement.GetRawText())), cancellationToken).ConfigureAwait(false);
        await AddUsersAsync(platformRole.RootElement, gateways.RootElement[0].GetProperty("id").GetString()!, cancellationToken).ConfigureAwait(false);
    }

    internal async Task DisableAlphaAsync(CancellationToken cancellationToken)
    {
        using var current = await SendAsync(HttpMethod.Get, "organizations/" + AlphaId, body: null, cancellationToken).ConfigureAwait(false);
        var organization = JsonNode.Parse(current.RootElement.GetRawText())!.AsObject();
        organization["enabled"] = false;
        using var updated = await SendAsync(HttpMethod.Put, "organizations/" + AlphaId, organization, cancellationToken).ConfigureAwait(false);
    }

    private async Task AddEnrollmentAsync(Uri gateway, CancellationToken cancellationToken)
    {
        var definition = new JsonObject
        {
            ["clientId"] = "identity-enrollment",
            ["enabled"] = true,
            ["publicClient"] = true,
            ["standardFlowEnabled"] = true,
            ["directAccessGrantsEnabled"] = false,
            ["redirectUris"] = new JsonArray(new Uri(gateway, "/enrolled").ToString()),
            ["attributes"] = new JsonObject { ["pkce.code.challenge.method"] = "S256" },
            ["defaultClientScopes"] = new JsonArray("basic", "profile", "email"),
        };
        using var created = await SendAsync(HttpMethod.Post, "clients", definition, cancellationToken).ConfigureAwait(false);
    }

    private async Task AddUsersAsync(JsonElement platformRole, string ordinaryClientId, CancellationToken cancellationToken)
    {
        AlphaId = await AddOrganizationAsync("alpha", cancellationToken).ConfigureAwait(false);
        BetaId = await AddOrganizationAsync("beta", cancellationToken).ConfigureAwait(false);
        await AddUserAsync("alpha-user", AlphaId, canWrite: true, cancellationToken).ConfigureAwait(false);
        await AddUserAsync("beta-user", BetaId, canWrite: true, cancellationToken).ConfigureAwait(false);
        await AddUserAsync("denied-user", AlphaId, canWrite: false, cancellationToken).ConfigureAwait(false);
        await AddUserAsync("admin-user", organization: null, canWrite: true, cancellationToken).ConfigureAwait(false);
        using var admins = await SendAsync(HttpMethod.Get, "users?username=admin-user&exact=true", body: null, cancellationToken).ConfigureAwait(false);
        var adminUser = JsonNode.Parse(admins.RootElement[0].GetRawText())!.AsObject();
        adminUser["requiredActions"] = new JsonArray("CONFIGURE_TOTP");
        var adminId = admins.RootElement[0].GetProperty("id").GetString();
        using var enroll = await SendAsync(HttpMethod.Put, "users/" + adminId, adminUser, cancellationToken).ConfigureAwait(false);
        using var granted = await SendAsync(HttpMethod.Post, "users/" + adminId + "/role-mappings/realm", new JsonArray(JsonNode.Parse(platformRole.GetRawText())), cancellationToken).ConfigureAwait(false);
        await AddUserAsync("privileged-customer", AlphaId, canWrite: true, cancellationToken).ConfigureAwait(false);
        using var privileged = await SendAsync(HttpMethod.Get, "users?username=privileged-customer&exact=true", body: null, cancellationToken).ConfigureAwait(false);
        using var privilegedRole = await SendAsync(HttpMethod.Post, "users/" + privileged.RootElement[0].GetProperty("id").GetString() + "/role-mappings/realm", new JsonArray(JsonNode.Parse(platformRole.GetRawText())), cancellationToken).ConfigureAwait(false);
        using var ordinaryScope = await SendAsync(HttpMethod.Post, "clients/" + ordinaryClientId + "/scope-mappings/realm", new JsonArray(JsonNode.Parse(platformRole.GetRawText())), cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> AddOrganizationAsync(string alias, CancellationToken cancellationToken)
    {
        using var created = await SendAsync(HttpMethod.Post, "organizations", new JsonObject { ["name"] = alias, ["alias"] = alias, ["enabled"] = true }, cancellationToken).ConfigureAwait(false);
        using var rows = await SendAsync(HttpMethod.Get, "organizations?search=" + alias + "&exact=true", body: null, cancellationToken).ConfigureAwait(false);
        return rows.RootElement[0].GetProperty("id").GetString()!;
    }

    private async Task AddUserAsync(string username, string? organization, bool canWrite, CancellationToken cancellationToken)
    {
        var definition = new JsonObject
        {
            ["username"] = username,
            ["email"] = username + "@example.test",
            ["firstName"] = "Identity",
            ["lastName"] = "Proof",
            ["enabled"] = true,
            ["emailVerified"] = true,
            ["credentials"] = new JsonArray(new JsonObject { ["type"] = "password", ["value"] = UserPassword, ["temporary"] = false }),
        };
        using var created = await SendAsync(HttpMethod.Post, "users", definition, cancellationToken).ConfigureAwait(false);
        using var users = await SendAsync(HttpMethod.Get, "users?username=" + username + "&exact=true", body: null, cancellationToken).ConfigureAwait(false);
        var userId = users.RootElement[0].GetProperty("id").GetString();
        if (organization is not null)
        {
            using var membership = await SendAsync(HttpMethod.Post, "organizations/" + organization + "/members", JsonValue.Create(userId), cancellationToken).ConfigureAwait(false);
        }

        if (canWrite)
        {
            using var role = await SendAsync(HttpMethod.Get, "roles/fixture-records-writer", body: null, cancellationToken).ConfigureAwait(false);
            using var assigned = await SendAsync(HttpMethod.Post, "users/" + userId + "/role-mappings/realm", new JsonArray(JsonNode.Parse(role.RootElement.GetRawText())), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, JsonNode? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, "admin/realms/teck-local/" + path);
        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonDocument.Parse(string.IsNullOrEmpty(content) ? "null" : content);
    }
}
