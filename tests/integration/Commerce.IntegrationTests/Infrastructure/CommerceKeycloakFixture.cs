using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Networks;
using Teck.Cloud.Commerce.Contracts;
using Testcontainers.Keycloak;
using Testcontainers.PostgreSql;
using Xunit;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>Disposable Keycloak with the production realm plus test-only commerce principals.</summary>
public sealed class CommerceKeycloakFixture : IAsyncLifetime
{
    private const string Realm = "teck-local";
    private const string CustomerLoginClient = "commerce-customer-login";
    private const string AdminLoginClient = "commerce-admin-login";
    private static readonly string[] CommerceRoles =
    [
        "CommerceOrdersRead",
        "CommerceOrdersDecide",
        "CommercePlacementRecord",
    ];

    private readonly string _adminPassword = RandomNumberGenerator.GetHexString(48);
    private readonly string _userPassword = RandomNumberGenerator.GetHexString(48);
    private readonly KeycloakContainer _container;
    private readonly PostgreSqlContainer _database;
    private readonly INetwork _network = new NetworkBuilder().Build();
    private readonly HttpClient _http = new(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    private readonly Dictionary<string, string> _usernames = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _stackGate = new(1, 1);
    private CommerceStack? _stack;

    /// <summary>Initializes a new instance of the <see cref="CommerceKeycloakFixture"/> class from the built theme image.</summary>
    public CommerceKeycloakFixture()
    {
        using var runtime = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Identity", "runtime.json")));
        var image = runtime.RootElement.GetProperty("image").GetString()
            ?? throw new InvalidOperationException("The Keycloak fixture image is missing.");
        var databasePassword = RandomNumberGenerator.GetHexString(48);
        _database = new PostgreSqlBuilder("postgres:18-alpine")
            .WithNetwork(_network)
            .WithNetworkAliases("commerce-identity-db")
            .WithDatabase("keycloak")
            .WithUsername("identity")
            .WithPassword(databasePassword)
            .Build();
        var builder = new KeycloakBuilder(image)
            .WithPassword(_adminPassword)
            .WithNetwork(_network)
            .WithEnvironment("KC_DB", "postgres")
            .WithEnvironment("KC_DB_URL", "jdbc:postgresql://commerce-identity-db:5432/keycloak")
            .WithEnvironment("KC_DB_USERNAME", "identity")
            .WithEnvironment("KC_DB_PASSWORD", databasePassword)
            .WithEnvironment("KC_HTTP_ENABLED", "true")
            .WithEnvironment("KC_HOSTNAME_STRICT", "false")
            .WithEnvironment("TECK_CUSTOMER_GATEWAY_SECRET", CustomerGatewayClientSecret)
            .WithEnvironment("TECK_ADMIN_GATEWAY_SECRET", AdminGatewayClientSecret)
            .WithEnvironment("TECK_TENANT_READER_SECRET", TenantReaderSecret)
            .WithRealm(Path.Combine(AppContext.BaseDirectory, "Identity", "Realms", "teck-local-realm.json"));
        if (runtime.RootElement.TryGetProperty("optimized", out var optimized) && optimized.GetBoolean())
        {
            builder = builder.WithCommand(new OverwriteEnumerable<string>(["start", "--optimized", "--import-realm"]));
        }

        _container = builder.Build();
    }

    /// <summary>Gets the disposable server base address.</summary>
    public Uri ServerUrl => new(_container.GetBaseAddress());

    /// <summary>Gets the canonical issuer minted into access tokens.</summary>
    public Uri Issuer => new(ServerUrl, "realms/" + Realm);

    /// <summary>Gets the disposable realm token endpoint.</summary>
    public Uri TokenEndpoint => new(new Uri(_container.GetBaseAddress()), "realms/" + Realm + "/protocol/openid-connect/token");

    /// <summary>Gets the confidential customer gateway client identifier used for exchange.</summary>
    public string CustomerGatewayClientId { get; } = "customer-gateway";

    /// <summary>Gets the confidential customer gateway secret used for exchange.</summary>
    public string CustomerGatewayClientSecret { get; } = RandomNumberGenerator.GetHexString(48);

    /// <summary>Gets the customer gateway access-token audience.</summary>
    public string CustomerAudience => CustomerGatewayClientId;

    /// <summary>Gets the test-only least-privileged tenant directory reader secret.</summary>
    public string TenantReaderSecret { get; } = RandomNumberGenerator.GetHexString(48);

    /// <summary>Gets the direct-grant client that mints customer tokens.</summary>
    public string CustomerLoginClientId { get; } = CustomerLoginClient;

    /// <summary>Gets the direct-grant client that mints staff tokens.</summary>
    public string AdminLoginClientId { get; } = AdminLoginClient;

    /// <summary>Gets the confidential admin gateway client identifier used for exchange.</summary>
    public string AdminGatewayClientId { get; } = "admin-gateway";

    /// <summary>Gets the confidential admin gateway secret used for exchange.</summary>
    public string AdminGatewayClientSecret { get; } = RandomNumberGenerator.GetHexString(48);

    /// <summary>Gets the admin gateway access-token audience.</summary>
    public string AdminAudience => AdminGatewayClientId;

    /// <summary>Gets the Licensing workload client identifier.</summary>
    public string LicensingClientId { get; } = "commerce-licensing";

    /// <summary>Gets the Licensing workload client secret.</summary>
    public string InternalClientSecret { get; } = RandomNumberGenerator.GetHexString(48);

    /// <summary>Gets customer A in tenant one.</summary>
    public CustomerAccount CustomerA { get; private set; } = new(Guid.Empty, string.Empty);

    /// <summary>Gets customer B in tenant one.</summary>
    public CustomerAccount CustomerB { get; private set; } = new(Guid.Empty, string.Empty);

    /// <summary>Gets customer C in tenant two.</summary>
    public CustomerAccount CustomerC { get; private set; } = new(Guid.Empty, string.Empty);

    /// <summary>Gets a tenant-one customer without the commerce permission.</summary>
    public CustomerAccount CustomerWithoutCommercePermission { get; private set; } = new(Guid.Empty, string.Empty);

    /// <summary>Gets the full staff principal allowed to read, decide and record evidence.</summary>
    public StaffAccount StaffFull { get; private set; } = new(string.Empty, string.Empty);

    /// <summary>Gets the read-only staff principal.</summary>
    public StaffAccount StaffReadOnly { get; private set; } = new(string.Empty, string.Empty);

    /// <summary>Gets tenant one's immutable organization identifier.</summary>
    public Guid TenantOneId { get; private set; }

    /// <summary>Gets tenant two's immutable organization identifier.</summary>
    public Guid TenantTwoId { get; private set; }

    /// <summary>Gets the tenant one organization alias used for direct-grant selection.</summary>
    public string TenantOneAlias { get; } = "commerce-tenant-one";

    /// <summary>Gets the tenant two organization alias used for direct-grant selection.</summary>
    public string TenantTwoAlias { get; } = "commerce-tenant-two";

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(4));
        await _network.CreateAsync(deadline.Token).ConfigureAwait(false);
        await _database.StartAsync(deadline.Token).ConfigureAwait(false);
        await _container.StartAsync(deadline.Token).ConfigureAwait(false);
        await ProvisionAsync(deadline.Token).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_stack is not null)
        {
            await _stack.DisposeAsync().ConfigureAwait(false);
        }

        _stackGate.Dispose();
        _http.Dispose();
        await _container.DisposeAsync().ConfigureAwait(false);
        await _database.DisposeAsync().ConfigureAwait(false);
        await _network.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Mints a real direct-grant customer token for the account's tenant and subject.</summary>
    /// <param name="account">The authoritative fixture customer.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The token and its canonical account.</returns>
    public async Task<CustomerToken> CustomerTokenAsync(CustomerAccount account, CancellationToken cancellationToken = default)
    {
        var username = _usernames[account.SubjectId];
        var alias = account.TenantId == TenantOneId ? TenantOneAlias : TenantTwoAlias;
        var token = await LoginAsync(CustomerLoginClient, username, "openid organization:" + alias, cancellationToken).ConfigureAwait(false);
        return new CustomerToken(token, account);
    }

    /// <summary>Mints a real tenant-one customer token without the commerce permission.</summary>
    /// <param name="account">The recipient account; its tenant selects the organization.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>A token that authenticates but lacks the commerce permission.</returns>
    public Task<string> CustomerTokenWithoutCommercePermissionAsync(CustomerAccount account, CancellationToken cancellationToken = default)
    {
        _ = account;
        return LoginAsync(CustomerLoginClient, _usernames[CustomerWithoutCommercePermission.SubjectId], "openid organization:" + TenantOneAlias, cancellationToken);
    }

    /// <summary>Mints a real staff token for the admin gateway audience.</summary>
    /// <param name="account">The staff principal.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The access token.</returns>
    public Task<string> StaffTokenAsync(StaffAccount account, CancellationToken cancellationToken = default) =>
        LoginAsync(AdminLoginClient, account.Username, scope: null, cancellationToken);

    /// <summary>Starts the shared real commerce stack once for the collection.</summary>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The running stack.</returns>
    internal async Task<CommerceStack> StackAsync(CancellationToken cancellationToken)
    {
        if (_stack is not null)
        {
            return _stack;
        }

        await _stackGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _stack ??= await CommerceStack.StartAsync(this, cancellationToken).ConfigureAwait(false);
            return _stack;
        }
        finally
        {
            _ = _stackGate.Release();
        }
    }

    private async Task ProvisionAsync(CancellationToken cancellationToken)
    {
        await AuthenticateAdminAsync(cancellationToken).ConfigureAwait(false);
        await CreateRealmRolesAsync(cancellationToken).ConfigureAwait(false);
        var (customerGatewayId, adminGatewayId) = await CreateClientsAsync(cancellationToken).ConfigureAwait(false);
        await ProvisionTenancyAsync(customerGatewayId, adminGatewayId, cancellationToken).ConfigureAwait(false);
    }

    private async Task AuthenticateAdminAsync(CancellationToken cancellationToken)
    {
        using var admin = await GrantAsync(
            new Uri(new Uri(_container.GetBaseAddress()), "realms/master/protocol/openid-connect/token"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "password",
                ["client_id"] = "admin-cli",
                ["username"] = "admin",
                ["password"] = _adminPassword,
            },
            cancellationToken).ConfigureAwait(false);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.RootElement.GetProperty("access_token").GetString());
    }

    private async Task CreateRealmRolesAsync(CancellationToken cancellationToken)
    {
        await CreateRealmRoleAsync("commerce.customer", cancellationToken).ConfigureAwait(false);
        await CreateRealmRoleAsync(CommerceAuth.LicenseReadPermission, cancellationToken).ConfigureAwait(false);
        await CreateRealmRoleAsync(CommerceAuth.EntitlementCheckPermission, cancellationToken).ConfigureAwait(false);
        foreach (var role in CommerceRoles)
        {
            await CreateRealmRoleAsync(role, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<(string CustomerGatewayId, string AdminGatewayId)> CreateClientsAsync(CancellationToken cancellationToken)
    {
        await CreateClientAsync(CustomerLoginClient, publicClient: true, directGrants: true, audience: CustomerGatewayClientId, cancellationToken).ConfigureAwait(false);
        await CreateClientAsync(AdminLoginClient, publicClient: true, directGrants: true, audience: AdminGatewayClientId, cancellationToken).ConfigureAwait(false);
        await CreateClientAsync("commerce-orders", publicClient: false, directGrants: false, audience: null, cancellationToken).ConfigureAwait(false);
        await CreateClientAsync("commerce-licensing", publicClient: false, directGrants: false, audience: null, cancellationToken, serviceAccounts: true, secret: InternalClientSecret).ConfigureAwait(false);

        var customerGatewayId = await ClientIdAsync(CustomerGatewayClientId, cancellationToken).ConfigureAwait(false);
        var adminGatewayId = await ClientIdAsync(AdminGatewayClientId, cancellationToken).ConfigureAwait(false);
        await AddAudienceMapperAsync(customerGatewayId, "orders-audience", "commerce-orders", cancellationToken).ConfigureAwait(false);
        await AddAudienceMapperAsync(customerGatewayId, "licensing-audience", "commerce-licensing", cancellationToken).ConfigureAwait(false);
        await AddAudienceMapperAsync(adminGatewayId, "orders-audience", "commerce-orders", cancellationToken).ConfigureAwait(false);
        await AddAudienceMapperAsync(adminGatewayId, "licensing-audience", "commerce-licensing", cancellationToken).ConfigureAwait(false);
        var licensingId = await ClientIdAsync("commerce-licensing", cancellationToken).ConfigureAwait(false);
        await AddAudienceMapperAsync(licensingId, "orders-audience", "commerce-orders", cancellationToken).ConfigureAwait(false);

        // The imported gateways have full scope disabled; expose only their intended realm roles.
        await AddRealmRoleScopeAsync(customerGatewayId, CommerceAuth.CustomerPermission, cancellationToken).ConfigureAwait(false);
        await AddRealmRoleScopeAsync(customerGatewayId, CommerceAuth.LicenseReadPermission, cancellationToken).ConfigureAwait(false);
        foreach (var role in CommerceRoles)
        {
            await AddRealmRoleScopeAsync(adminGatewayId, role, cancellationToken).ConfigureAwait(false);
        }

        await EnableServiceAccountAsync(customerGatewayId, cancellationToken).ConfigureAwait(false);
        await AssignServiceAccountRealmRoleAsync(customerGatewayId, CommerceAuth.LicenseReadPermission, cancellationToken).ConfigureAwait(false);
        await AssignServiceAccountRealmRoleAsync(licensingId, CommerceAuth.EntitlementCheckPermission, cancellationToken).ConfigureAwait(false);
        return (customerGatewayId, adminGatewayId);
    }

    private async Task ProvisionTenancyAsync(string customerGatewayId, string adminGatewayId, CancellationToken cancellationToken)
    {
        // Client roles mirror the contracts' named staff policies for hosts that bind authorization services.
        foreach (var role in CommerceRoles)
        {
            await CreateClientRoleAsync(customerGatewayId, role, cancellationToken).ConfigureAwait(false);
            await CreateClientRoleAsync(adminGatewayId, role, cancellationToken).ConfigureAwait(false);
        }

        await CreateClientRoleAsync(customerGatewayId, "commerce.customer", cancellationToken).ConfigureAwait(false);

        var tenantOne = await CreateOrganizationAsync(TenantOneAlias, "Commerce tenant one", cancellationToken).ConfigureAwait(false);
        var tenantTwo = await CreateOrganizationAsync(TenantTwoAlias, "Commerce tenant two", cancellationToken).ConfigureAwait(false);
        TenantOneId = tenantOne;
        TenantTwoId = tenantTwo;

        var customerA = await CreateUserAsync("commerce-customer-a", ["commerce.customer"], cancellationToken).ConfigureAwait(false);
        var customerB = await CreateUserAsync("commerce-customer-b", ["commerce.customer"], cancellationToken).ConfigureAwait(false);
        var customerC = await CreateUserAsync("commerce-customer-c", ["commerce.customer"], cancellationToken).ConfigureAwait(false);
        var customerD = await CreateUserAsync("commerce-customer-denied", [], cancellationToken).ConfigureAwait(false);
        var staffFull = await CreateUserAsync("commerce-staff-full", CommerceRoles, cancellationToken).ConfigureAwait(false);
        var staffReadOnly = await CreateUserAsync("commerce-staff-read", ["CommerceOrdersRead"], cancellationToken).ConfigureAwait(false);

        await AddMemberAsync(tenantOne, customerA, cancellationToken).ConfigureAwait(false);
        await AddMemberAsync(tenantOne, customerB, cancellationToken).ConfigureAwait(false);
        await AddMemberAsync(tenantOne, customerD, cancellationToken).ConfigureAwait(false);
        await AddMemberAsync(tenantTwo, customerC, cancellationToken).ConfigureAwait(false);

        await AssignClientRoleAsync(customerGatewayId, customerA, "commerce.customer", cancellationToken).ConfigureAwait(false);
        await AssignClientRoleAsync(customerGatewayId, customerB, "commerce.customer", cancellationToken).ConfigureAwait(false);
        await AssignClientRoleAsync(customerGatewayId, customerC, "commerce.customer", cancellationToken).ConfigureAwait(false);
        foreach (var role in CommerceRoles)
        {
            await AssignClientRoleAsync(adminGatewayId, staffFull, role, cancellationToken).ConfigureAwait(false);
        }

        await AssignClientRoleAsync(adminGatewayId, staffReadOnly, "CommerceOrdersRead", cancellationToken).ConfigureAwait(false);

        CustomerA = new CustomerAccount(TenantOneId, customerA);
        CustomerB = new CustomerAccount(TenantOneId, customerB);
        CustomerC = new CustomerAccount(TenantTwoId, customerC);
        CustomerWithoutCommercePermission = new CustomerAccount(TenantOneId, customerD);
        StaffFull = new StaffAccount(_usernames[staffFull], staffFull);
        StaffReadOnly = new StaffAccount(_usernames[staffReadOnly], staffReadOnly);
    }

    private async Task<string> LoginAsync(string clientId, string username, string? scope, CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["username"] = username,
            ["password"] = _userPassword,
        };
        if (scope is not null)
        {
            fields["scope"] = scope;
        }

        using var token = await GrantAsync(TokenEndpoint, fields, cancellationToken).ConfigureAwait(false);
        return token.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("The identity provider returned no access token.");
    }

    private async Task<JsonDocument> GrantAsync(Uri endpoint, Dictionary<string, string> fields, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new FormUrlEncodedContent(fields) };
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var disposeStream = stream.ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task CreateRealmRoleAsync(string name, CancellationToken cancellationToken) =>
        await PostAsync("roles", new JsonObject { ["name"] = name }, cancellationToken).ConfigureAwait(false);

    private async Task CreateClientRoleAsync(string clientId, string name, CancellationToken cancellationToken) =>
        await PostAsync("clients/" + clientId + "/roles", new JsonObject { ["name"] = name }, cancellationToken).ConfigureAwait(false);

    private async Task CreateClientAsync(string clientId, bool publicClient, bool directGrants, string? audience, CancellationToken cancellationToken, bool serviceAccounts = false, string? secret = null)
    {
        var definition = new JsonObject
        {
            ["clientId"] = clientId,
            ["enabled"] = true,
            ["publicClient"] = publicClient,
            ["standardFlowEnabled"] = false,
            ["directAccessGrantsEnabled"] = directGrants,
            ["serviceAccountsEnabled"] = serviceAccounts,
            ["defaultClientScopes"] = new JsonArray("basic", "profile", "email", "roles"),
        };
        if (publicClient)
        {
            definition["optionalClientScopes"] = new JsonArray("organization");
        }

        if (secret is not null)
        {
            definition["secret"] = secret;
        }

        if (audience is not null)
        {
            definition["protocolMappers"] = new JsonArray(new JsonObject
            {
                ["name"] = "gateway-audience",
                ["protocol"] = "openid-connect",
                ["protocolMapper"] = "oidc-audience-mapper",
                ["config"] = new JsonObject
                {
                    ["included.client.audience"] = audience,
                    ["access.token.claim"] = "true",
                },
            });
        }

        using var created = await PostAsync("clients", definition, cancellationToken).ConfigureAwait(false);
        _ = created;
    }

    private async Task EnableServiceAccountAsync(string clientId, CancellationToken cancellationToken)
    {
        using var client = await GetAsync("clients/" + clientId, cancellationToken).ConfigureAwait(false);
        var body = JsonNode.Parse(client.RootElement.GetRawText())!.AsObject();
        body["serviceAccountsEnabled"] = true;
        using var request = new HttpRequestMessage(HttpMethod.Put, AdminUri("clients/" + clientId))
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
    }

    private async Task AssignServiceAccountRealmRoleAsync(string clientId, string role, CancellationToken cancellationToken)
    {
        using var serviceAccount = await GetAsync("clients/" + clientId + "/service-account-user", cancellationToken).ConfigureAwait(false);
        var userId = serviceAccount.RootElement.GetProperty("id").GetString();
        using var realmRole = await GetAsync("roles/" + Uri.EscapeDataString(role), cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            AdminUri("users/" + userId + "/role-mappings/realm"))
        {
            Content = new StringContent(new JsonArray(JsonNode.Parse(realmRole.RootElement.GetRawText())).ToJsonString(), Encoding.UTF8, "application/json"),
        };
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
    }

    private async Task AddRealmRoleScopeAsync(string clientId, string role, CancellationToken cancellationToken)
    {
        using var definition = await GetAsync("roles/" + Uri.EscapeDataString(role), cancellationToken).ConfigureAwait(false);
        using var mapping = await PostAsync(
            "clients/" + clientId + "/scope-mappings/realm",
            new JsonArray(JsonNode.Parse(definition.RootElement.GetRawText())),
            cancellationToken).ConfigureAwait(false);
        _ = mapping;
    }

    private async Task AddAudienceMapperAsync(string clientId, string name, string audience, CancellationToken cancellationToken) =>
        await PostAsync(
            "clients/" + clientId + "/protocol-mappers/models",
            new JsonObject
            {
                ["name"] = name,
                ["protocol"] = "openid-connect",
                ["protocolMapper"] = "oidc-audience-mapper",
                ["config"] = new JsonObject
                {
                    ["included.client.audience"] = audience,
                    ["access.token.claim"] = "true",
                },
            },
            cancellationToken).ConfigureAwait(false);

    private async Task<string> ClientIdAsync(string clientId, CancellationToken cancellationToken)
    {
        using var clients = await GetAsync("clients?clientId=" + Uri.EscapeDataString(clientId), cancellationToken).ConfigureAwait(false);
        return clients.RootElement.EnumerateArray().Single().GetProperty("id").GetString()
            ?? throw new InvalidOperationException("The Keycloak client has no identifier.");
    }

    private async Task<Guid> CreateOrganizationAsync(string alias, string name, CancellationToken cancellationToken)
    {
        using var created = await PostAsync("organizations", new JsonObject { ["name"] = name, ["alias"] = alias, ["enabled"] = true }, cancellationToken).ConfigureAwait(false);
        _ = created;
        using var organizations = await GetAsync("organizations", cancellationToken).ConfigureAwait(false);
        var id = organizations.RootElement.EnumerateArray()
            .Single(item => string.Equals(item.GetProperty("alias").GetString(), alias, StringComparison.Ordinal))
            .GetProperty("id").GetString();
        return Guid.Parse(id!);
    }

    private async Task<string> CreateUserAsync(string username, IReadOnlyList<string> roles, CancellationToken cancellationToken)
    {
        var user = new JsonObject
        {
            ["username"] = username,
            ["email"] = username + "@example.test",
            ["firstName"] = "Commerce",
            ["lastName"] = "Fixture",
            ["emailVerified"] = true,
            ["enabled"] = true,
            ["credentials"] = new JsonArray(new JsonObject
            {
                ["type"] = "password",
                ["value"] = _userPassword,
                ["temporary"] = false,
            }),
        };
        using var created = await PostAsync("users", user, cancellationToken).ConfigureAwait(false);
        _ = created;
        using var users = await GetAsync("users?username=" + Uri.EscapeDataString(username) + "&exact=true", cancellationToken).ConfigureAwait(false);
        var id = users.RootElement.EnumerateArray().Single().GetProperty("id").GetString()
            ?? throw new InvalidOperationException("The Keycloak user has no identifier.");
        _usernames[id] = username;
        foreach (var role in roles)
        {
            using var realmRole = await GetAsync("roles/" + Uri.EscapeDataString(role), cancellationToken).ConfigureAwait(false);
            using var assigned = await PostAsync("users/" + id + "/role-mappings/realm", new JsonArray(JsonNode.Parse(realmRole.RootElement.GetRawText())), cancellationToken).ConfigureAwait(false);
            _ = assigned;
        }

        return id;
    }

    private async Task AddMemberAsync(Guid organizationId, string userId, CancellationToken cancellationToken)
    {
        using var member = await PostAsync("organizations/" + organizationId.ToString("D") + "/members", JsonValue.Create(userId), cancellationToken).ConfigureAwait(false);
        _ = member;
    }

    private async Task AssignClientRoleAsync(string clientId, string userId, string role, CancellationToken cancellationToken)
    {
        using var definition = await GetAsync("clients/" + clientId + "/roles/" + Uri.EscapeDataString(role), cancellationToken).ConfigureAwait(false);
        using var assigned = await PostAsync(
            "users/" + userId + "/role-mappings/clients/" + clientId,
            new JsonArray(JsonNode.Parse(definition.RootElement.GetRawText())),
            cancellationToken).ConfigureAwait(false);
        _ = assigned;
    }

    private async Task<JsonDocument> PostAsync(string path, JsonNode? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, AdminUri(path));
        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Conflict)
        {
            return JsonDocument.Parse("null");
        }

        _ = response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(string.IsNullOrEmpty(text) ? "null" : text);
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(AdminUri(path), cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var disposeStream = stream.ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private Uri AdminUri(string path) => new(new Uri(_container.GetBaseAddress()), "admin/realms/" + Realm + "/" + path);
}
