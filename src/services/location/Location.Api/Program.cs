// <copyright file="Program.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using FluentValidation;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Common;
using Location.Api.Extensions;
using Location.Application;
using Location.Infrastructure.DependencyInjection;
using Location.Infrastructure.Service;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;
using SharedKernel.Infrastructure.Options;

namespace Location.Api;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        Assembly applicationAssembly = typeof(ILocationApplication).Assembly;
        Assembly apiAssembly = typeof(Program).Assembly;

        AppOptions appOptions = new();
        builder.Configuration.GetSection(AppOptions.Section).Bind(appOptions);

        builder.AddServiceDefaults();
        builder.AddBaseInfrastructure(appOptions);
        builder.Services.Configure<TemplateFontStorageOptions>(builder.Configuration.GetSection(TemplateFontStorageOptions.Section));
        builder.AddInfrastructureServices(applicationAssembly);
        builder.Services.AddFastEndpointsInfrastructure(applicationAssembly, apiAssembly);
        builder.Services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);
        builder.Services.AddValidatorsFromAssembly(apiAssembly, includeInternalTypes: true);
        builder.AddMediatorInfrastructure(applicationAssembly);
        builder.AddOpenApiInfrastructure(appOptions);
        ConfigureIdentity(builder);

        WebApplication app = builder.Build();

        app.UseBaseInfrastructure();
        app.UseInfrastructureServices();
        app.UseFastEndpointsInfrastructure("location");
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
}
