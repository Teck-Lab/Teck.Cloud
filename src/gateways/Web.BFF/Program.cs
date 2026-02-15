using System.Reflection;
using Keycloak.AuthServices.Authentication;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Infrastructure.OpenApi;
using SharedKernel.Infrastructure.Options;
using Web.BFF.Middleware.InternalTrust;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Configuration sections
var keycloakSection = builder.Configuration.GetSection("Keycloak");
Assembly applicationAssembly = typeof(Web.BFF.Services.TokenExchangeService).Assembly;
Assembly apiAssembly = typeof(Web.BFF.Services.TokenExchangeService).Assembly;
var appOptions = new AppOptions();
builder.Configuration.GetSection(AppOptions.Section).Bind(appOptions);

// Add Keycloak auth (uses SharedKernel helper)
var keycloakOptions = new KeycloakAuthenticationOptions();
keycloakSection.Bind(keycloakOptions);

builder.Services.AddKeycloak(builder.Configuration, builder.Environment, keycloakOptions);

// Add Finbuckle MultiTenant (basic wiring)
// Use the shared kernel multi-tenant extension if available
builder.Services.AddTeckCloudMultiTenancy();

// FusionCache usage: register FusionCache (shared extensions expect IFusionCache)
builder.Services.AddFusionCache();

// YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHttpClient("KeycloakTokenClient", client =>
{
});

builder.Services.AddHttpClient("CustomerApi", client =>
{
    var customerApiUrl = builder.Configuration["Services:CustomerApi:Url"];
    if (string.IsNullOrWhiteSpace(customerApiUrl))
    {
        customerApiUrl = builder.Configuration["ReverseProxy:Clusters:customer:Destinations:cluster1:Address"];
    }

    if (!string.IsNullOrWhiteSpace(customerApiUrl))
    {
        client.BaseAddress = new Uri(customerApiUrl);
    }
});

builder.Services.AddHttpClient("CatalogApi", client =>
{
    var catalogApiUrl = builder.Configuration["Services:CatalogApi:Url"];
    if (string.IsNullOrWhiteSpace(catalogApiUrl))
    {
        catalogApiUrl = builder.Configuration["ReverseProxy:Clusters:catalog:Destinations:cluster1:Address"];
    }

    if (!string.IsNullOrWhiteSpace(catalogApiUrl))
    {
        client.BaseAddress = new Uri(catalogApiUrl);
    }
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<Web.BFF.Services.ITokenExchangeService, Web.BFF.Services.TokenExchangeService>();
builder.Services.AddSingleton<Web.BFF.Services.ITenantRoutingMetadataService, Web.BFF.Services.TenantRoutingMetadataService>();
builder.Services.AddFastEndpointsInfrastructure(applicationAssembly, apiAssembly);
builder.AddOpenApiInfrastructure(appOptions);

// Authentication/Authorization middleware (Keycloak)
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpointsInfrastructure();
app.UseOpenApiInfrastructure(appOptions);

app.UseMiddleware<InternalIdentityValidationMiddleware>();

// Token exchange middleware should run before ReverseProxy so it can mutate headers
app.UseMiddleware<Web.BFF.Middleware.TokenExchangeMiddleware>();

app.MapReverseProxy();

app.Run();

