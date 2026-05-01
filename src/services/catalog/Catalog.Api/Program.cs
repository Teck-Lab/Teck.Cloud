// <copyright file="Program.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Catalog.Api.Extensions;
using Catalog.Api.Grpc.V1;
using Catalog.Api.Infrastructure.Messaging.Tenants;
using Catalog.Application;
using Catalog.Infrastructure.DependencyInjection;
using FastEndpoints;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using FluentValidation;
using JasperFx;
using SharedKernel.Grpc.Contracts.Remote.V1.Tenants;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Caching;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;
using SharedKernel.Infrastructure.Options;
using SharedKernel.Persistence.Database.MultiTenant;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
bool isRunningWolverineCodeGeneration = CodeGenerationDetector.IsRunningWolverineCodeGeneration();
builder.ConfigureInternalServiceTransport();
builder.AddServiceDefaults();

// Add multi-tenant support BEFORE infrastructure services
builder.AddCachingInfrastructure();
builder.AddMultiTenantSupport();

Assembly applicationAssembly = typeof(ICatalogApplication).Assembly;
var appOptions = new AppOptions();
builder.Configuration.GetSection(AppOptions.Section).Bind(appOptions);

builder.AddBaseInfrastructure(appOptions);
builder.AddInfrastructureServices(applicationAssembly);
builder.Services.AddSingleton<ICatalogTenantDatabaseInfoClient, CatalogTenantDatabaseInfoClient>();
if (!isRunningWolverineCodeGeneration)
{
    builder.Services.AddSingleton<CatalogTenantConnectionMissResolver>();
}

builder.Services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);
builder.Services.AddFastEndpointsInfrastructure(applicationAssembly, typeof(Program).Assembly);
builder.AddOpenApiInfrastructure(appOptions);

builder.AddMediatorInfrastructure(applicationAssembly);

builder.Services.AddRequestTimeouts();
builder.AddHandlerServer();

WebApplication app = builder.Build();

string customerApiRemoteAddress = ResolveRemoteAddress(
    builder.Configuration,
    "Services:CustomerApi:Url");

app.MapRemote(
    customerApiRemoteAddress,
    remote =>
    {
        remote.Register<GetTenantDatabaseInfoCommand, TenantDatabaseInfoRpcResult>();
    });

if (isRunningWolverineCodeGeneration)
{
    MapRemoteHandlers(app);
    await app.RunJasperFxCommands(args).ConfigureAwait(false);
    return;
}

ConfigureTenantMissResolution(app);

app.UseMultiTenant();

app.UseBaseInfrastructure();
app.UseInfrastructureServices();
app.UseRequestTimeouts();
app.UseFastEndpointsInfrastructure("catalog");
app.UseOpenApiInfrastructure(appOptions);
MapRemoteHandlers(app);
app.MapDefaultEndpoints();

await app.RunJasperFxCommands(args).ConfigureAwait(false);

static void MapRemoteHandlers(WebApplication app)
{
    app.MapHandlers(handlerRegistry =>
    {
        handlerRegistry.Register<SharedKernel.Grpc.Contracts.Remote.V1.Catalog.ValidateProductsForBasketCommand, ValidateProductsForBasketCommandHandler, SharedKernel.Grpc.Contracts.Remote.V1.Catalog.ValidateProductsForBasketRpcResult>();
    });
}

static string ResolveRemoteAddress(IConfiguration configuration, string key)
{
    string? value = configuration[key];
    if (TryBuildAbsoluteUri(value, out Uri uri))
    {
        return uri.ToString();
    }

    throw new InvalidOperationException($"Missing valid remote address. Configure '{key}'.");
}

static bool TryBuildAbsoluteUri(string? value, out Uri uri)
{
    uri = default!;
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    string normalized = value.Trim();
    if (Uri.TryCreate(normalized, UriKind.Absolute, out Uri? parsed) && parsed is not null)
    {
        uri = parsed;
        return true;
    }

    return false;
}

static void ConfigureTenantMissResolution(WebApplication app)
{
    WolverineTenantConnectionSource tenantConnectionSource = app.Services.GetRequiredService<WolverineTenantConnectionSource>();
    CatalogTenantConnectionMissResolver resolver = app.Services.GetRequiredService<CatalogTenantConnectionMissResolver>();
    bool strictTenantIsolation = app.Configuration.GetValue<bool>("Messaging:StrictTenantIsolation");

    tenantConnectionSource.SetStrictTenantResolution(strictTenantIsolation);
    tenantConnectionSource.SetMissingTenantResolver(resolver.ResolveAsync);
}
