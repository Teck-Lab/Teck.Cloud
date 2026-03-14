var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("redis");

var postgresWrite = builder.AddPostgres("db-write")
    .WithPgAdmin()
    .WithDataVolume(isReadOnly: false);

var catalogDb_postgresWrite = postgresWrite.AddDatabase("catalogdb");
_ = postgresWrite.AddDatabase("sitedb");
_ = postgresWrite.AddDatabase("devicedb");
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

var catalogapi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(cache)
    .WithReference(catalogDb_postgresWrite, "db-write")
    .WithReference(catalogDb_postgresWrite, "db-read")
    .WithReference(rabbitmq)
    .WaitFor(cache)
    .WaitFor(rabbitmq)
    .WithEnvironment("ASPIRE_LOCAL", "true");

var customerapi = builder.AddProject<Projects.Customer_Api>("customer-api")
    .WithReference(cache)
    .WithReference(customerdb_postgresWrite, "db-write")
    .WithReference(customerdb_postgresWrite, "db-read")
    .WithReference(rabbitmq)
    .WaitFor(cache)
    .WaitFor(rabbitmq)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPIRE_LOCAL", "true");

var aggregateGateway = builder.AddProject<Projects.Web_Aggregate_Gateway>("web-aggregate-gateway")
    .WithReference(cache)
    .WithReference(rabbitmq)
    .WaitFor(cache)
    .WaitFor(rabbitmq)
    .WithEnvironment("ASPIRE_LOCAL", "true");

var edgeGateway = builder.AddProject<Projects.Web_Edge>("web-edge")
    .WithReference(cache)
    .WithReference(rabbitmq)
    .WaitFor(cache)
    .WaitFor(rabbitmq)
    .WithEnvironment("ASPIRE_LOCAL", "true");

catalogapi.WithReference(customerapi).WaitFor(customerapi);
aggregateGateway.WithReference(customerapi).WaitFor(customerapi);
aggregateGateway.WithReference(catalogapi).WaitFor(catalogapi);
edgeGateway.WithReference(aggregateGateway).WaitFor(aggregateGateway);
edgeGateway.WithReference(customerapi).WaitFor(customerapi);
edgeGateway.WithReference(catalogapi).WaitFor(catalogapi);

catalogapi.WithEnvironment("Services__CustomerApi__Url", customerapi.GetEndpoint("https"));

aggregateGateway
    .WithEnvironment("Services__CatalogApi__Url", catalogapi.GetEndpoint("https"))
    .WithEnvironment("Services__CustomerApi__Url", customerapi.GetEndpoint("https"));

edgeGateway
    .WithEnvironment("Services__CustomerApi__Url", customerapi.GetEndpoint("https"))
    .WithEnvironment("ReverseProxy__Clusters__aggregate-gateway__Destinations__Default__Address", aggregateGateway.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__catalog__Destinations__Default__Address", catalogapi.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__customer__Destinations__Default__Address", customerapi.GetEndpoint("http"));

// Configure multi-tenant settings for Keycloak nested organization claims
// These will be passed to the API projects as environment variables
Dictionary<string, string> multiTenantSettings = new()
{
    { "MultiTenancy:UseClaimStrategy", "true" },
    { "MultiTenancy:UseHeaderStrategy", "true" },
    { "MultiTenancy:OrganizationClaimName", "organization" },
    { "MultiTenancy:TenantIdClaimName", "tenant_id" },
    { "MultiTenancy:MultiTenantClaimName", "tenant_ids" },
    { "MultiTenancy:MultiTenantResolutionStrategy", "FromRequest" }
};

// Apply multi-tenant settings to API projects and migration service
foreach (var setting in multiTenantSettings)
{
    // API projects
    catalogapi.WithEnvironment(setting.Key, setting.Value);
    customerapi.WithEnvironment(setting.Key, setting.Value);
    aggregateGateway.WithEnvironment(setting.Key, setting.Value);
    edgeGateway.WithEnvironment(setting.Key, setting.Value);
}

await builder.Build().RunAsync();
