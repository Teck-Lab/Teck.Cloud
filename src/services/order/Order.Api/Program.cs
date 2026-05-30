// <copyright file="Program.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using FastEndpoints;
using FluentValidation;
using Order.Api.Extensions;
using Order.Application;
using Order.Infrastructure.DependencyInjection;
using SharedKernel.Grpc.Contracts.Remote.V1.Catalog;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;
using SharedKernel.Infrastructure.Options;

namespace Order.Api;

/// <summary>
/// Order API entry point.
/// </summary>
internal static class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        Assembly applicationAssembly = typeof(IOrderApplication).Assembly;
        Assembly apiAssembly = typeof(Program).Assembly;

        builder.AddServiceDefaults();
        AppOptions appOptions = new();
        builder.Configuration.GetSection(AppOptions.Section).Bind(appOptions);
        builder.AddBaseInfrastructure(appOptions);
        builder.Services.AddFastEndpointsInfrastructure(applicationAssembly, apiAssembly);
        builder.AddOpenApiInfrastructure(appOptions);
        builder.Services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);
        builder.Services.AddValidatorsFromAssembly(apiAssembly, includeInternalTypes: true);
        builder.AddMediatorInfrastructure(applicationAssembly);
        builder.Services.AddInfrastructureServices(builder.Configuration);

        WebApplication app = builder.Build();
        string catalogApiRemoteAddress = ResolveRemoteAddress(builder.Configuration, "Services:CatalogApi:Url");

        app.MapRemote(
            catalogApiRemoteAddress,
            remote =>
            {
                remote.Register<ValidateProductsForBasketCommand, ValidateProductsForBasketRpcResult>();
            });

        app.UseBaseInfrastructure();
        app.UseFastEndpointsInfrastructure("order");
        app.UseOpenApiInfrastructure(appOptions);
        app.MapDefaultEndpoints();

        await app.RunAsync().ConfigureAwait(false);
    }

    private static string ResolveRemoteAddress(IConfiguration configuration, string key)
    {
        string? value = configuration[key];
        if (TryBuildAbsoluteUri(value, out Uri uri))
        {
            return uri.ToString();
        }

        throw new InvalidOperationException($"Missing valid remote address. Configure '{key}'.");
    }

    private static bool TryBuildAbsoluteUri(string? value, out Uri uri)
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
}
