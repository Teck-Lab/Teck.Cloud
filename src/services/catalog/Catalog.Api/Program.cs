// <copyright file="Program.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Catalog.Api.Extensions;
using Catalog.Api.Grpc.V1;
using Catalog.Application;
using Catalog.Infrastructure.DependencyInjection;
using FastEndpoints;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using FluentValidation;
using JasperFx;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using SharedKernel.Grpc.Contracts.Remote.V1.ServiceVersions;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Caching;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;
using SharedKernel.Infrastructure.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });
});

builder.AddServiceDefaults();

// Add multi-tenant support BEFORE infrastructure services
builder.AddCachingInfrastructure();
builder.AddMultiTenantSupport();

Assembly applicationAssembly = typeof(ICatalogApplication).Assembly;
var appOptions = new AppOptions();
builder.Configuration.GetSection(AppOptions.Section).Bind(appOptions);

builder.AddBaseInfrastructure(appOptions);
builder.AddInfrastructureServices(applicationAssembly);

builder.Services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);
builder.Services.AddFastEndpointsInfrastructure(applicationAssembly, typeof(Program).Assembly);
builder.AddOpenApiInfrastructure(appOptions);

builder.AddMediatorInfrastructure(applicationAssembly);

builder.Services.AddRequestTimeouts();
builder.AddHandlerServer();

WebApplication app = builder.Build();

app.UseMultiTenant();

app.UseBaseInfrastructure();
app.UseInfrastructureServices();
app.UseRequestTimeouts();
app.UseFastEndpointsInfrastructure("catalog");
app.UseOpenApiInfrastructure(appOptions);
app.MapHandlers(handlerRegistry =>
{
    handlerRegistry.Register<GetCatalogServiceVersionCommand, GetCatalogServiceVersionCommandHandler, ServiceVersionRpcResult>();
});
app.MapDefaultEndpoints();

await app.RunJasperFxCommands(args).ConfigureAwait(false);
