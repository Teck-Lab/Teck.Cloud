// <copyright file="Program.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using FastEndpoints;
using FluentValidation;
using Product.Api.Extensions;
using Product.Api.Grpc.V1;
using Product.Application;
using Product.Infrastructure.DependencyInjection;
using SharedKernel.Grpc.Contracts.Remote.V1.Products;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;
using SharedKernel.Infrastructure.Options;

namespace Product.Api;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        Assembly applicationAssembly = typeof(IProductApplication).Assembly;
        Assembly apiAssembly = typeof(Program).Assembly;

        AppOptions appOptions = new();
        builder.Configuration.GetSection(AppOptions.Section).Bind(appOptions);

        builder.ConfigureInternalServiceTransport();
        builder.AddServiceDefaults();
        builder.AddBaseInfrastructure(appOptions);
        builder.AddInfrastructureServices(applicationAssembly);
        builder.Services.AddFastEndpointsInfrastructure(applicationAssembly, apiAssembly);
        builder.Services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);
        builder.Services.AddValidatorsFromAssembly(apiAssembly, includeInternalTypes: true);
        builder.AddMediatorInfrastructure(applicationAssembly);
        builder.AddOpenApiInfrastructure(appOptions);
        builder.AddHandlerServer();

        WebApplication app = builder.Build();

        app.UseBaseInfrastructure();
        app.UseInfrastructureServices();
        app.UseFastEndpointsInfrastructure("product");
        app.UseOpenApiInfrastructure(appOptions);
        app.MapHandlers(handlerRegistry =>
        {
            handlerRegistry.Register<GetProductSnapshotsCommand, GetProductSnapshotsCommandHandler, GetProductSnapshotsRpcResult>();
        });
        app.MapDefaultEndpoints();

        await app.RunAsync().ConfigureAwait(false);
    }
}
