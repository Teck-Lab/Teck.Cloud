// <copyright file="Program.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Basket.Api.Extensions;
using Basket.Application;
using Basket.Infrastructure.DependencyInjection;
using FastEndpoints;
using FluentValidation;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Common;
using SharedKernel.Grpc.Contracts.Remote.V1.Catalog;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.Caching;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.Options;

namespace Basket.Api;

/// <summary>
/// Basket API entry point.
/// </summary>
internal static class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        Assembly applicationAssembly = typeof(IBasketApplication).Assembly;
        Assembly apiAssembly = typeof(Program).Assembly;
        AppOptions appOptions = new();
        builder.Configuration.GetSection(AppOptions.Section).Bind(appOptions);

        builder.AddServiceDefaults();
        builder.AddBaseInfrastructure(appOptions);
        ConfigureIdentity(builder);
        builder.AddCachingInfrastructure();
        builder.Services.AddFastEndpointsInfrastructure(applicationAssembly, apiAssembly);
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
        app.UseFastEndpointsInfrastructure("basket");
        app.MapDefaultEndpoints();

        await app.RunAsync().ConfigureAwait(false);
    }

    private static void ConfigureIdentity(WebApplicationBuilder builder)
    {
        if (!TryGetKeycloakOptions(builder.Configuration, out KeycloakAuthenticationOptions? keycloakOptions) || keycloakOptions is null)
        {
            return;
        }

        builder.Services.AddKeycloak(builder.Configuration, builder.Environment, keycloakOptions);
    }

    private static bool TryGetKeycloakOptions(ConfigurationManager configuration, out KeycloakAuthenticationOptions? keycloakOptions)
    {
        keycloakOptions = null;

        string? keycloakAuthServerUrl = configuration["Keycloak:AuthServerUrl"]
            ?? configuration["Keycloak:auth-server-url"]
            ?? configuration["Keycloak:Authority"];

        if (string.IsNullOrWhiteSpace(keycloakAuthServerUrl) ||
            !Uri.IsWellFormedUriString(keycloakAuthServerUrl, UriKind.Absolute))
        {
            return false;
        }

        keycloakOptions = configuration.GetKeycloakOptions<KeycloakAuthenticationOptions>();

        return keycloakOptions is not null &&
               !string.IsNullOrWhiteSpace(keycloakOptions.KeycloakUrlRealm) &&
               Uri.IsWellFormedUriString(keycloakOptions.KeycloakUrlRealm, UriKind.Absolute);
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
