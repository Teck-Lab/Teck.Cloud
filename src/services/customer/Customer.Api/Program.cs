// <copyright file="Program.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Customer.Api.Extensions;
using Customer.Api.Grpc.V1;
using Customer.Api.Infrastructure.Messaging.Tenants;
using Customer.Application;
using Customer.Infrastructure.DependencyInjection;
using FastEndpoints;
using FluentValidation;
using JasperFx;
using SharedKernel.Grpc.Contracts.Remote.V1.Tenants;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;
using SharedKernel.Infrastructure.Options;
using SharedKernel.Persistence.Database.MultiTenant;

namespace Customer.Api;

/// <summary>
/// Application entry point.
/// </summary>
internal static class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = CreateBuilder(args);
        bool isRunningWolverineCodeGeneration = CodeGenerationDetector.IsRunningWolverineCodeGeneration();
        Assembly applicationAssembly = typeof(ICustomerApplication).Assembly;
        AppOptions appOptions = BuildAppOptions(builder);
        ConfigureServices(builder, applicationAssembly, appOptions);
        WebApplication app = BuildApp(builder, appOptions, isRunningWolverineCodeGeneration);
        await app.RunJasperFxCommands(args).ConfigureAwait(false);
    }

    private static WebApplicationBuilder CreateBuilder(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.ConfigureInternalServiceTransport();
        builder.AddServiceDefaults();
        return builder;
    }

    [RequiresDynamicCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.Bind(Object)")]
    [RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.Bind(Object)")]
    private static AppOptions BuildAppOptions(WebApplicationBuilder builder)
    {
        AppOptions appOptions = new();
        builder.Configuration.GetSection(AppOptions.Section).Bind(appOptions);
        return appOptions;
    }

    private static void ConfigureServices(WebApplicationBuilder builder, Assembly applicationAssembly, AppOptions appOptions)
    {
        Assembly apiAssembly = typeof(Program).Assembly;
        builder.AddBaseInfrastructure(appOptions);
        builder.AddInfrastructureServices(applicationAssembly);
        builder.Services.AddSingleton<CustomerTenantConnectionMissResolver>();
        builder.Services.AddFastEndpointsInfrastructure(applicationAssembly, apiAssembly);
        builder.AddOpenApiInfrastructure(appOptions);
        AddValidation(builder, applicationAssembly, apiAssembly);
        builder.AddMediatorInfrastructure(applicationAssembly);
        AddCoreServices(builder);
    }

    private static void AddValidation(WebApplicationBuilder builder, Assembly applicationAssembly, Assembly apiAssembly)
    {
        builder.Services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);
        builder.Services.AddValidatorsFromAssembly(apiAssembly, includeInternalTypes: true);
    }

    private static void AddCoreServices(WebApplicationBuilder builder)
    {
        builder.Services.AddRequestTimeouts();
        builder.AddHandlerServer();
    }

    private static WebApplication BuildApp(WebApplicationBuilder builder, AppOptions appOptions, bool isRunningWolverineCodeGeneration)
    {
        WebApplication app = builder.Build();

        if (isRunningWolverineCodeGeneration)
        {
            MapRemoteHandlers(app);
            return app;
        }

        ConfigureTenantMissResolution(app);

        app.UseBaseInfrastructure();
        app.UseInfrastructureServices();
        app.UseRequestTimeouts();
        app.UseFastEndpointsInfrastructure("customer");
        app.UseOpenApiInfrastructure(appOptions);
        MapRemoteHandlers(app);
        app.MapDefaultEndpoints();
        return app;
    }

    private static void MapRemoteHandlers(WebApplication app)
    {
        app.MapHandlers(handlerRegistry =>
        {
            handlerRegistry.Register<GetTenantDatabaseInfoCommand, GetTenantDatabaseInfoCommandHandler, TenantDatabaseInfoRpcResult>();
            handlerRegistry.Register<GetTenantConnectionSeedsCommand, GetTenantConnectionSeedsCommandHandler, TenantConnectionSeedsRpcResult>();
        });
    }

    private static void ConfigureTenantMissResolution(WebApplication app)
    {
        WolverineTenantConnectionSource tenantConnectionSource = app.Services.GetRequiredService<WolverineTenantConnectionSource>();
        CustomerTenantConnectionMissResolver resolver = app.Services.GetRequiredService<CustomerTenantConnectionMissResolver>();
        bool strictTenantIsolation = app.Configuration.GetValue<bool>("Messaging:StrictTenantIsolation");

        tenantConnectionSource.SetStrictTenantResolution(strictTenantIsolation);
        tenantConnectionSource.SetMissingTenantResolver(resolver.ResolveAsync);
    }
}
