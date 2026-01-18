using System.Reflection;
using Catalog.Api.Extensions;
using Catalog.Application;
using Catalog.Infrastructure.DependencyInjection;
using Finbuckle.MultiTenant;
using JasperFx;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Caching;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.Idempotency;
using SharedKernel.Infrastructure.OpenApi;
using SharedKernel.Infrastructure.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var customerApiUrlString = builder.Configuration["Services:CustomerApi:Url"];
Uri? customerApiUrl = !string.IsNullOrEmpty(customerApiUrlString) ? new Uri(customerApiUrlString) : null;

// Add multi-tenant support BEFORE infrastructure services
builder.AddCachingInfrastructure();
builder.AddMultiTenantSupport(customerApiUrl);

Assembly applicationAssembly = typeof(ICatalogApplication).Assembly;
var appOptions = new AppOptions();
builder.Configuration.GetSection(AppOptions.Section).Bind(appOptions);

builder.AddBaseInfrastructure(appOptions);
builder.AddInfrastructureServices(applicationAssembly);

builder.Services.AddFastEndpointsInfrastructure(applicationAssembly);
builder.AddMediatorInfrastructure(applicationAssembly);
builder.Services.AddIdempotencySupport();
builder.AddOpenApiInfrastructure(appOptions);

builder.Services.AddRequestTimeouts();
builder.Services.AddOutputCache();

WebApplication app = builder.Build();

app.UseMultiTenant();
app.UseBaseInfrastructure();
app.UseInfrastructureServices();
app.UseRequestTimeouts();
app.UseFastEndpointsInfrastructure();
app.UseOpenApiInfrastructure(appOptions);

app.MapDefaultEndpoints();

await app.RunJasperFxCommands(args);
