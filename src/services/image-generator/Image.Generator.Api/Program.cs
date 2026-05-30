// <copyright file="Program.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using FastEndpoints;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using FluentValidation;
using Image.Generator.Api.Extensions;
using Image.Generator.Api.Grpc.V1;
using Image.Generator.Application;
using Image.Generator.Application.Render.Features.SubmitRenderJob.V1;
using SharedKernel.Grpc.Contracts.Remote.V1.Labels;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Caching;

using SharedKernel.Infrastructure.Messaging;
using SharedKernel.Infrastructure.Options;
using Wolverine;

namespace Image.Generator.Api;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        Assembly applicationAssembly = typeof(IImageGeneratorApplication).Assembly;
        Assembly apiAssembly = typeof(Program).Assembly;

        AppOptions appOptions = new();
        builder.Configuration.GetSection(AppOptions.Section).Bind(appOptions);

        builder.ConfigureInternalServiceTransport();
        builder.AddServiceDefaults();
        builder.AddMultiTenantSupport();
        builder.AddBaseInfrastructure(appOptions);
        builder.AddCachingInfrastructure();
        builder.Services.Configure<RenderProcessingOptions>(builder.Configuration.GetSection(RenderProcessingOptions.Section));
        builder.Services.Configure<RenderFontOptions>(builder.Configuration.GetSection(RenderFontOptions.Section));
        builder.Services.AddSingleton<RenderConcurrencyLimiter>();
        builder.Services.AddHttpClient<ITenantFontAssetStore, TenantFontAssetStore>();
        builder.Services.AddFluentImageStorage(builder.Configuration);

        builder.Services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);
        builder.Services.AddValidatorsFromAssembly(apiAssembly, includeInternalTypes: true);
        builder.AddMediatorInfrastructure(applicationAssembly);
        builder.AddHandlerServer();

        string? rabbitConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
        if (!string.IsNullOrWhiteSpace(rabbitConnectionString))
        {
            string normalizedRabbit = WolverinePersistenceConfigurator.NormalizeRabbitConnectionString(rabbitConnectionString);
            bool isDevelopment = builder.Environment.IsDevelopment();

            builder.Host.UseWolverine(options =>
            {
                options.Discovery.IncludeAssembly(applicationAssembly);
                WolverinePersistenceConfigurator.ConfigureStatelessRuntime(options, isDevelopment, normalizedRabbit);
            });
        }

        WebApplication app = builder.Build();

        app.UseMultiTenant();
        app.UseBaseInfrastructure();

        app.MapHandlers(handlerRegistry =>
        {
            handlerRegistry.Register<EnqueueRenderJobCommand, EnqueueRenderJobCommandHandler, EnqueueRenderJobRpcResult>();
        });
        app.MapDefaultEndpoints();

        await app.RunAsync().ConfigureAwait(false);
    }
}
