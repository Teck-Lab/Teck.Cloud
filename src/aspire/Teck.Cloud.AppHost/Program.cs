var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("redis");

var postgresWrite = builder.AddPostgres("db-write")
    .WithPgAdmin()
    .WithDataVolume(isReadOnly: false);

var catalogDb_postgresWrite = postgresWrite.AddDatabase("catalogdb");
_ = postgresWrite.AddDatabase("sitedb");
var deviceDb_postgresWrite = postgresWrite.AddDatabase("devicedb");
var locationDb_postgresWrite = postgresWrite.AddDatabase("locationdb");
var productDb_postgresWrite = postgresWrite.AddDatabase("productdb");
var billingDb_postgresWrite = postgresWrite.AddDatabase("billingdb");
var customerdb_postgresWrite = postgresWrite.AddDatabase("customerdb");
var basketdb_postgresWrite = postgresWrite.AddDatabase("basketdb");

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

var catalogMigrations = builder.AddProject<Projects.Teck_Cloud_Migrations>("catalog-migrations")
    .WithEnvironment("CONNECTION_STRING", catalogDb_postgresWrite)
    .WithArgs("--service", "catalog")
    .WaitFor(postgresWrite);

var customerMigrations = builder.AddProject<Projects.Teck_Cloud_Migrations>("customer-migrations")
    .WithEnvironment("CONNECTION_STRING", customerdb_postgresWrite)
    .WithArgs("--service", "customer")
    .WaitFor(postgresWrite);

var catalogapi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(cache)
    .WithReference(catalogDb_postgresWrite, "db-write")
    .WithReference(catalogDb_postgresWrite, "db-read")
    .WithReference(rabbitmq)
    .WaitFor(cache)
    .WaitFor(rabbitmq)
    .WaitForCompletion(catalogMigrations)
    .WithEnvironment("ASPIRE_LOCAL", "true");

var customerapi = builder.AddProject<Projects.Customer_Api>("customer-api")
    .WithReference(cache)
    .WithReference(customerdb_postgresWrite, "db-write")
    .WithReference(customerdb_postgresWrite, "db-read")
    .WithReference(rabbitmq)
    .WaitFor(cache)
    .WaitFor(rabbitmq)
    .WaitForCompletion(customerMigrations)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPIRE_LOCAL", "true");

var deviceMigrations = builder.AddProject<Projects.Teck_Cloud_Migrations>("device-migrations")
    .WithEnvironment("CONNECTION_STRING", deviceDb_postgresWrite)
    .WithArgs("--service", "device")
    .WaitFor(postgresWrite);

var locationMigrations = builder.AddProject<Projects.Teck_Cloud_Migrations>("location-migrations")
    .WithEnvironment("CONNECTION_STRING", locationDb_postgresWrite)
    .WithArgs("--service", "location")
    .WaitFor(postgresWrite);

var productMigrations = builder.AddProject<Projects.Teck_Cloud_Migrations>("product-migrations")
    .WithEnvironment("CONNECTION_STRING", productDb_postgresWrite)
    .WithArgs("--service", "product")
    .WaitFor(postgresWrite);

var billingMigrations = builder.AddProject<Projects.Teck_Cloud_Migrations>("billing-migrations")
    .WithEnvironment("CONNECTION_STRING", billingDb_postgresWrite)
    .WithArgs("--service", "billing")
    .WaitFor(postgresWrite);

var basketapi = builder.AddProject<Projects.Basket_Api>("basket-api")
    .WithReference(cache)
    .WithReference(basketdb_postgresWrite, "db-write")
    .WithReference(rabbitmq)
    .WaitFor(cache)
    .WaitFor(rabbitmq)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPIRE_LOCAL", "true");

var deviceapi = builder.AddProject<Projects.Device_Api>("device-api")
    .WithReference(cache)
    .WithReference(deviceDb_postgresWrite, "db-write")
    .WithReference(deviceDb_postgresWrite, "db-read")
    .WithReference(rabbitmq)
    .WaitFor(cache)
    .WaitFor(rabbitmq)
    .WaitForCompletion(deviceMigrations)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPIRE_LOCAL", "true");

_ = builder.AddProject<Projects.Device_VendorWorker>("device-vendor-worker")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPIRE_LOCAL", "true");

var locationapi = builder.AddProject<Projects.Location_Api>("location-api")
    .WithReference(locationDb_postgresWrite, "db-write")
    .WithReference(locationDb_postgresWrite, "db-read")
    .WaitForCompletion(locationMigrations)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPIRE_LOCAL", "true");

var productapi = builder.AddProject<Projects.Product_Api>("product-api")
    .WithReference(productDb_postgresWrite, "db-write")
    .WithReference(productDb_postgresWrite, "db-read")
    .WaitForCompletion(productMigrations)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPIRE_LOCAL", "true");

var statisticapi = builder.AddProject<Projects.Statistic_Api>("statistic-api")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPIRE_LOCAL", "true");

var billingapi = builder.AddProject<Projects.Billing_Api>("billing-api")
    .WithReference(billingDb_postgresWrite, "db-write")
    .WaitForCompletion(billingMigrations)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPIRE_LOCAL", "true");

var orderapi = builder.AddProject<Projects.Order_Api>("order-api")
    .WithReference(basketapi)
    .WithReference(catalogapi)
    .WaitFor(basketapi)
    .WaitFor(catalogapi)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPIRE_LOCAL", "true");

var imageGeneratorApi = builder.AddProject<Projects.Image_Generator_Api>("image-generator")
    .WithReference(cache)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPIRE_LOCAL", "true");

var edgeGateway = builder.AddProject<Projects.Web_Public_Gateway>("web-public-gateway")
    .WithReference(cache)
    .WithReference(rabbitmq)
    .WaitFor(cache)
    .WaitFor(rabbitmq)
    .WithEnvironment("ASPIRE_LOCAL", "true");

var adminGateway = builder.AddProject<Projects.Web_Admin_Gateway>("web-admin-gateway")
    .WithEnvironment("ASPIRE_LOCAL", "true");

catalogapi.WithReference(customerapi).WaitFor(customerapi);
basketapi.WithReference(catalogapi).WaitFor(catalogapi);
edgeGateway.WithReference(customerapi);
edgeGateway.WithReference(catalogapi);
edgeGateway.WithReference(deviceapi);
edgeGateway.WithReference(locationapi);
edgeGateway.WithReference(basketapi);
edgeGateway.WithReference(orderapi);
edgeGateway.WithReference(productapi);
edgeGateway.WithReference(statisticapi);
edgeGateway.WithReference(imageGeneratorApi);
edgeGateway.WithReference(billingapi);
adminGateway.WithReference(customerapi).WaitFor(customerapi);
adminGateway.WithReference(catalogapi).WaitFor(catalogapi);
adminGateway.WithReference(deviceapi).WaitFor(deviceapi);
adminGateway.WithReference(locationapi).WaitFor(locationapi);
adminGateway.WithReference(productapi).WaitFor(productapi);
adminGateway.WithReference(statisticapi).WaitFor(statisticapi);
adminGateway.WithReference(billingapi).WaitFor(billingapi);

catalogapi.WithEnvironment("Services__CustomerApi__Url", customerapi.GetEndpoint("https"));
basketapi.WithEnvironment("Services__CatalogApi__Url", catalogapi.GetEndpoint("https"));
orderapi
    .WithEnvironment("Services__CatalogApi__Url", catalogapi.GetEndpoint("https"))
    .WithEnvironment("Services__BasketBaseUrl", basketapi.GetEndpoint("http"));

edgeGateway
    .WithEnvironment("Services__CustomerApi__Url", customerapi.GetEndpoint("https"))
    .WithEnvironment("ReverseProxy__Clusters__catalog__Destinations__Default__Address", catalogapi.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__customer__Destinations__Default__Address", customerapi.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__device__Destinations__Default__Address", deviceapi.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__location__Destinations__Default__Address", locationapi.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__basket__Destinations__Default__Address", basketapi.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__order__Destinations__Default__Address", orderapi.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__product__Destinations__Default__Address", productapi.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__statistic__Destinations__Default__Address", statisticapi.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__imagegenerator__Destinations__Default__Address", imageGeneratorApi.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__billing__Destinations__Default__Address", billingapi.GetEndpoint("http"));

adminGateway
    .WithEnvironment("ReverseProxy__Clusters__catalog__Destinations__Default__Address", catalogapi.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__customer__Destinations__Default__Address", customerapi.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__device__Destinations__Default__Address", deviceapi.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__location__Destinations__Default__Address", locationapi.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__product__Destinations__Default__Address", productapi.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__statistic__Destinations__Default__Address", statisticapi.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__billing__Destinations__Default__Address", billingapi.GetEndpoint("http"));

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
    edgeGateway.WithEnvironment(setting.Key, setting.Value);
    adminGateway.WithEnvironment(setting.Key, setting.Value);
}

await builder.Build().RunAsync();
