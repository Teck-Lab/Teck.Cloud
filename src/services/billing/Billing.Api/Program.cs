// <copyright file="Program.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Billing.Api.Extensions;
using Billing.Application;
using Billing.Infrastructure.DependencyInjection;
using FastEndpoints;
using FluentValidation;
using JasperFx;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;
using SharedKernel.Infrastructure.Options;

namespace Billing.Api;

/// <summary>
/// Application entry point.
/// </summary>
internal static class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = CreateBuilder(args);
        bool isRunningWolverineCodeGeneration = CodeGenerationDetector.IsRunningWolverineCodeGeneration();
        Assembly applicationAssembly = typeof(IBillingApplication).Assembly;
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

    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.Bind(Object)")]
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.Bind(Object)")]
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
        builder.Services.AddFastEndpointsInfrastructure(applicationAssembly, apiAssembly);
        builder.AddOpenApiInfrastructure(appOptions);
        builder.Services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);
        builder.Services.AddValidatorsFromAssembly(apiAssembly, includeInternalTypes: true);
        builder.AddMediatorInfrastructure(applicationAssembly);
        builder.Services.AddRequestTimeouts();
        builder.AddHandlerServer();
    }

    private static WebApplication BuildApp(WebApplicationBuilder builder, AppOptions appOptions, bool isRunningWolverineCodeGeneration)
    {
        WebApplication app = builder.Build();

        if (isRunningWolverineCodeGeneration)
        {
            return app;
        }

        app.UseBaseInfrastructure();
        app.UseInfrastructureServices();
        app.UseRequestTimeouts();
        app.UseFastEndpointsInfrastructure("billing");
        app.UseOpenApiInfrastructure(appOptions);
        app.MapDefaultEndpoints();
        return app;
    }
}
