// <copyright file="Program.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Device.Api.Extensions;
using Device.Application;
using Device.Infrastructure.DependencyInjection;
using FastEndpoints;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Common;
using SharedKernel.Grpc.Contracts.Remote.V1.Labels;
using SharedKernel.Grpc.Contracts.Remote.V1.Products;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;
using SharedKernel.Infrastructure.Options;

namespace Device.Api;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        Assembly applicationAssembly = typeof(IDeviceApplication).Assembly;
        Assembly apiAssembly = typeof(Program).Assembly;

        AppOptions appOptions = new();
        builder.Configuration.GetSection(AppOptions.Section).Bind(appOptions);

        builder.AddServiceDefaults();
        builder.AddMultiTenantSupport();
        builder.AddBaseInfrastructure(appOptions);
        builder.AddInfrastructureServices(applicationAssembly);
        builder.Services.AddFastEndpointsInfrastructure(applicationAssembly, apiAssembly);

        // Validators are auto-discovered by FastEndpoints; explicit registration removed for AOT compatibility
        builder.AddMediatorInfrastructure(applicationAssembly);
        builder.AddOpenApiInfrastructure(appOptions);
        ConfigureIdentity(builder);

        WebApplication app = builder.Build();

        string? productApiRemoteAddress = TryResolveRemoteAddress(builder.Configuration, "Services:ProductApi:Url");
        if (!string.IsNullOrWhiteSpace(productApiRemoteAddress))
        {
            app.MapRemote(
                productApiRemoteAddress,
                remote =>
                {
                    remote.Register<GetProductSnapshotsCommand, GetProductSnapshotsRpcResult>();
                });
        }
        else
        {
            app.Logger.LogWarning("Missing or invalid Services:ProductApi:Url. Product snapshot remote calls are disabled for this process.");
        }

        string? labelGeneratorApiRemoteAddress = TryResolveRemoteAddress(builder.Configuration, "Services:LabelGeneratorApi:Url");
        if (!string.IsNullOrWhiteSpace(labelGeneratorApiRemoteAddress))
        {
            app.MapRemote(
                labelGeneratorApiRemoteAddress,
                remote =>
                {
                    remote.Register<EnqueueRenderJobCommand, EnqueueRenderJobRpcResult>();
                });
        }

        app.UseMultiTenant();
        app.UseBaseInfrastructure();
        app.UseInfrastructureServices();
        app.UseFastEndpointsInfrastructure("device");
        app.UseOpenApiInfrastructure(appOptions);
        app.MapDefaultEndpoints();

        await app.RunAsync().ConfigureAwait(false);
    }

    private static void ConfigureIdentity(WebApplicationBuilder builder)
    {
        if (!TryGetKeycloakOptions(builder.Configuration, out KeycloakAuthenticationOptions? keycloakOptions) || keycloakOptions is null)
        {
            Console.WriteLine("[Startup] Keycloak not configured or authority invalid; skipping Keycloak registration for tests.");
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

    private static string? TryResolveRemoteAddress(IConfiguration configuration, string key)
    {
        string? value = configuration[key];
        if (TryBuildAbsoluteUri(value, out Uri uri))
        {
            return uri.ToString();
        }

        return null;
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
