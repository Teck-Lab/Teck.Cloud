using System.Security.Cryptography;
using System.Text;
using Keycloak.AuthServices.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.MultiTenant;
using Web.BFF.Middleware.InternalTrust;
using ZiggyCreatures.Caching.Fusion;
using Yarp.ReverseProxy.Transforms;
using FastEndpoints;
using FastEndpoints.Swagger;

var builder = WebApplication.CreateBuilder(args);

// Configuration sections
var keycloakSection = builder.Configuration.GetSection("Keycloak");

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
    if (!string.IsNullOrWhiteSpace(customerApiUrl))
    {
        client.BaseAddress = new Uri(customerApiUrl);
    }
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<Web.BFF.Services.ITokenExchangeService, Web.BFF.Services.TokenExchangeService>();
builder.Services.AddSingleton<Web.BFF.Services.ITenantRoutingMetadataService, Web.BFF.Services.TenantRoutingMetadataService>();

// FastEndpoints
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(options =>
{
    options.DocumentSettings = settings =>
    {
        settings.DocumentName = "v1";
        settings.Title = "Teck Web BFF";
        settings.Version = "v1";
    };
});

// Authentication/Authorization middleware (Keycloak)
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("ok"));

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<InternalIdentityValidationMiddleware>();

// Token exchange middleware should run before ReverseProxy so it can mutate headers
app.UseMiddleware<Web.BFF.Middleware.TokenExchangeMiddleware>();

// Use FastEndpoints for small auth endpoints
app.UseFastEndpoints();
app.UseSwaggerGen(swaggerOptions =>
{
    swaggerOptions.Path = "/openapi/{documentName}/openapi.json";
});

app.MapReverseProxy();

app.Run();

