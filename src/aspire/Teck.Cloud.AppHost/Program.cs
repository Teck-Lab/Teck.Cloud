var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("redis");

var postgresWrite = builder.AddPostgres("postgres-write")
    .WithPgAdmin()
    .WithDataVolume(isReadOnly: false);

var catalogDb_postgresWrite = postgresWrite.AddDatabase("catalogdb");
var sitedb_postgresWrite = postgresWrite.AddDatabase("sitedb");
var devicedb_postgresWrite = postgresWrite.AddDatabase("devicedb");
var customerdb_postgresWrite = postgresWrite.AddDatabase("customerdb");

var rabbitmqUserName = builder.CreateResourceBuilder(new ParameterResource(
    "guest",
    defaultValue => defaultValue?.GetDefaultValue() ?? "guest", // Use GetDefaultValue() instead of Value
    false
));
var rabbitmqPassword = builder.CreateResourceBuilder(new ParameterResource(
    "guest",
    defaultValue => defaultValue?.GetDefaultValue() ?? "guest", // Use GetDefaultValue() instead of Value
    false
));

var rabbitmq = builder.AddRabbitMQ("rabbitmq", rabbitmqUserName, rabbitmqPassword).WithManagementPlugin();

const string keycloakAdminUser = "admin";
const string keycloakAdminPassword = "admin";

var keycloak = builder.AddKeycloakContainer("keycloak", "0.14.1")
    .WithImage("teck-cloud/auth")
    .WithDockerfile("../../auth")
    .WithImport("../keycloak/realm-export(1).json")
    .WithEnvironment("KEYCLOAK_ADMIN", keycloakAdminUser)
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", keycloakAdminPassword)
    .WithDataVolume("local");

const string edgeTrustSigningKey = "local-edge-trust-signing-key-change-me-0123456789";
const string edgeTrustIssuer = "teck-edge";
const string edgeTrustAudience = "teck-web-bff-internal";


var catalogapi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(cache)
    .WithReference(catalogDb_postgresWrite, "postgres-write")
    .WithReference(rabbitmq)
    .WithReference(keycloak)
    .WaitFor(cache)
    .WaitFor(rabbitmq)
    .WithEnvironment("Services__CustomerApi__Url", "https+http://customer-api")
    .WithEnvironment("ASPIRE_LOCAL", "true");

var customerapi = builder.AddProject<Projects.Customer_Api>("customer-api")
    .WithReference(cache)
    .WithReference(customerdb_postgresWrite, "postgres-write")
    .WithReference(rabbitmq)
    .WithReference(keycloak)
    .WaitFor(cache)
    .WaitFor(rabbitmq)
    .WithEnvironment("ASPIRE_LOCAL", "true");

var webbff = builder.AddProject<Projects.Web_BFF>("web-bff")
    .WithReference(cache)
    .WithReference(rabbitmq)
    .WithReference(keycloak)
    .WaitFor(cache)
    .WaitFor(rabbitmq)
    .WithEnvironment("Services__CatalogApi__Url", "https+http://catalog-api")
    .WithEnvironment("Services__CustomerApi__Url", "https+http://customer-api")
    .WithEnvironment("ReverseProxy__Clusters__catalog__Destinations__cluster1__Address", "https+http://catalog-api")
    .WithEnvironment("EdgeTrust__SigningKey", edgeTrustSigningKey)
    .WithEnvironment("EdgeTrust__Issuer", edgeTrustIssuer)
    .WithEnvironment("EdgeTrust__Audience", edgeTrustAudience)
    .WithEnvironment("EdgeTrust__Enforce", "true")
    .WithEnvironment("ASPIRE_LOCAL", "true");

var webedge = builder.AddProject<Projects.Web_Edge>("web-edge")
    .WithReference(keycloak)
    .WithEnvironment("ReverseProxy__Clusters__bff__Destinations__cluster1__Address", "https+http://web-bff")
    .WithEnvironment("ReverseProxy__Clusters__catalog__Destinations__cluster1__Address", "https+http://catalog-api")
    .WithEnvironment("ReverseProxy__Clusters__customer__Destinations__cluster1__Address", "https+http://customer-api")
    .WithEnvironment("EdgeTrust__SigningKey", edgeTrustSigningKey)
    .WithEnvironment("EdgeTrust__Issuer", edgeTrustIssuer)
    .WithEnvironment("EdgeTrust__Audience", edgeTrustAudience)
    .WithEnvironment("EdgeTrust__LifetimeSeconds", "120")
    .WithEnvironment("ASPIRE_LOCAL", "true");

catalogapi.WithReference(customerapi).WaitFor(customerapi);
webbff.WithReference(customerapi).WaitFor(customerapi);
webbff.WithReference(catalogapi).WaitFor(catalogapi);
webedge.WithReference(webbff).WaitFor(webbff);
webedge.WithReference(catalogapi).WaitFor(catalogapi);
webedge.WithReference(customerapi).WaitFor(customerapi);

// Configure multi-tenant settings for Keycloak nested organization claims
// These will be passed to the API projects as environment variables
Dictionary<string, string> multiTenantSettings = new()
{
    { "MultiTenancy:UseClaimStrategy", "true" },
    { "MultiTenancy:UseHeaderStrategy", "true" },
    { "MultiTenancy:OrganizationClaimName", "organization" },
    { "MultiTenancy:TenantIdClaimName", "tenant_id" },
    { "MultiTenancy:MultiTenantClaimName", "tenant_ids" },
    { "MultiTenancy:MultiTenantResolutionStrategy", "Primary" }
};

// Apply multi-tenant settings to API projects and migration service
foreach (var setting in multiTenantSettings)
{
    // API projects
    catalogapi.WithEnvironment(setting.Key, setting.Value);
    customerapi.WithEnvironment(setting.Key, setting.Value);
    webbff.WithEnvironment(setting.Key, setting.Value);
}

await builder.Build().RunAsync();
