// <copyright file="Program.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Common;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.Options;
using Statistic.Api.Serialization;
using Statistic.Application;
using Statistic.Infrastructure.DependencyInjection;
using Statistic.Infrastructure.Hubs;
using Wolverine;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var appOptions = new AppOptions();
builder.Configuration.GetSection(AppOptions.Section).Bind(appOptions);

builder.AddBaseInfrastructure(appOptions);

builder.Host.UseWolverine(options => builder.ConfigureWolverine(options));
builder.AddInfrastructureServices(typeof(IStatisticApplication).Assembly);
builder.Services.AddSingleton<JsonSerializerContext, StatisticJsonSerializerContext>();
ConfigureIdentity(builder);

const string SignalRCorsPolicy = "SignalRCors";
string[] corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["https://dashboard.tecklab.dk", "https://localhost:3000"];
builder.Services.AddCors(options =>
{
    options.AddPolicy(SignalRCorsPolicy, policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

WebApplication app = builder.Build();

app.UseBaseInfrastructure();
app.UseInfrastructureServices();
app.UseCors(SignalRCorsPolicy);

app.MapHub<StatisticsHub>("/hubs/statistics").RequireAuthorization();

app.MapDefaultEndpoints();

await app.RunAsync().ConfigureAwait(false);

static void ConfigureIdentity(WebApplicationBuilder builder)
{
    if (!TryGetKeycloakOptions(builder.Configuration, out KeycloakAuthenticationOptions? keycloakOptions) || keycloakOptions is null)
    {
        Console.WriteLine("[Startup] Keycloak not configured or authority invalid; skipping Keycloak registration for tests.");
        return;
    }

    builder.Services.AddKeycloak(builder.Configuration, builder.Environment, keycloakOptions);
}

static bool TryGetKeycloakOptions(ConfigurationManager configuration, out KeycloakAuthenticationOptions? keycloakOptions)
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
