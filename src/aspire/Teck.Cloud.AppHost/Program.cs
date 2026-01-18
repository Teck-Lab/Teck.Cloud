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

var keycloak = builder.AddKeycloakContainer("keycloak", "26.5.1")
    .WithDataVolume("local");

var realm = keycloak.AddRealm("Teck-Cloud");


var catalogapi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(cache)
    .WithReference(catalogDb_postgresWrite, "postgres-write")
    .WithReference(rabbitmq)
    .WithReference(keycloak)
    .WithReference(realm)
    .WaitFor(cache)
    .WaitFor(rabbitmq);

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
}

await builder.Build().RunAsync();