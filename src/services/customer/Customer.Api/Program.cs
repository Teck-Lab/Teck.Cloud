using System.Reflection;
using Customer.Api.Extensions;
using Customer.Application;
using Customer.Infrastructure.DependencyInjection;
using JasperFx;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;
using SharedKernel.Infrastructure.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

Assembly applicationAssembly = typeof(ICustomerApplication).Assembly;
var appOptions = new AppOptions();
builder.Configuration.GetSection(AppOptions.Section).Bind(appOptions);

builder.AddBaseInfrastructure(appOptions);
builder.AddInfrastructureServices(applicationAssembly);

builder.Services.AddFastEndpointsInfrastructure(applicationAssembly);
builder.AddMediatorInfrastructure(applicationAssembly);
builder.AddOpenApiInfrastructure(appOptions);

builder.Services.AddRequestTimeouts();
builder.Services.AddOutputCache();

WebApplication app = builder.Build();

app.UseBaseInfrastructure();
app.UseInfrastructureServices();
app.UseRequestTimeouts();
app.UseFastEndpointsInfrastructure();
app.UseOpenApiInfrastructure(appOptions);

app.MapDefaultEndpoints();

await app.RunJasperFxCommands(args);
