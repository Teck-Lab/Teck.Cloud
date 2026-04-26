using FastEndpoints;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Common;
using Microsoft.AspNetCore.Authentication;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Infrastructure.Options;
using Web.Public.Gateway.Middleware;
using Web.Public.Gateway.Services;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var appOptions = new AppOptions();
builder.Configuration.GetSection(AppOptions.Section).Bind(appOptions);

builder.AddBaseInfrastructure(appOptions);

bool useMockAuthentication = builder.Configuration.GetValue<bool>("Testing:UseMockAuthentication")
    || (bool.TryParse(Environment.GetEnvironmentVariable("TECK_TEST_MOCK_AUTH"), out bool parsedUseMockAuthentication)
        && parsedUseMockAuthentication);

if (useMockAuthentication)
{
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "Bearer";
            options.DefaultChallengeScheme = "Bearer";
            options.DefaultScheme = "Bearer";
        })
        .AddScheme<AuthenticationSchemeOptions, MockBearerAuthenticationHandler>("Bearer", _ =>
        {
        });

    builder.Services.AddAuthorization();
}
else
{
    KeycloakAuthenticationOptions keycloakOptions = builder.Configuration.GetKeycloakOptions<KeycloakAuthenticationOptions>()
        ?? throw new InvalidOperationException("Keycloak configuration section is missing or invalid.");

    builder.Services.AddKeycloak(builder.Configuration, builder.Environment, keycloakOptions);
}

builder.Services.AddFusionCache();
builder.Services.AddSingleton<IServiceTokenExchangeService, ServiceTokenExchangeService>();
builder.Services.AddSingleton<ITenantTokenContextResolver, TenantTokenContextResolver>();
builder.Services.AddSingleton<ITenantDatabaseStrategyResolver, RemoteTenantDatabaseStrategyResolver>();
builder.Services.AddHttpClient("KeycloakTokenClient", client =>
{
});

EdgeTenantOptions edgeTenantOptions = builder.Configuration.GetEdgeTenantOptions();
builder.Services.AddSingleton(edgeTenantOptions);

EdgeRouteSecurityOptions edgeRouteSecurityOptions = builder.Configuration.GetEdgeRouteSecurityOptions();
builder.Services.AddSingleton(edgeRouteSecurityOptions);

IConfigurationSection reverseProxyConfiguration = builder.Configuration.GetSection("ReverseProxy");
builder.Services.AddReverseProxy()
    .LoadFromConfig(reverseProxyConfiguration)
    .AddServiceDiscoveryDestinationResolver()
    .AddEdgeGatewayTransforms(edgeTenantOptions);

builder.Services.AddOpenApi();

WebApplication app = builder.Build();

string customerApiRemoteAddress = ResolveRemoteAddress(
    builder.Configuration,
    "Services:CustomerApi:Url");

app.Logger.LogInformation(
    "Configuring tenant lookup RPC remote. ServiceName={ServiceName}; RemoteAddress={RemoteAddress}",
    "customer",
    customerApiRemoteAddress);

app.MapRemote(
    customerApiRemoteAddress,
    remote =>
    {
        remote.Register<SharedKernel.Grpc.Contracts.Remote.V1.Tenants.GetTenantDatabaseInfoCommand, SharedKernel.Grpc.Contracts.Remote.V1.Tenants.TenantDatabaseInfoRpcResult>();
    });

app.UseBaseInfrastructure();
app.UseRouting();
app.UseMiddleware<TenantEnforcementMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapOpenApi();
app.MapEdgeScalarApiReference(builder.Configuration);
app.MapReverseProxy();

await app.RunAsync();

static string ResolveRemoteAddress(IConfiguration configuration, string key)
{
    string? value = configuration[key];
    if (TryBuildAbsoluteUri(value, out Uri uri))
    {
        return uri.ToString();
    }

    throw new InvalidOperationException($"Missing valid remote address. Configure '{key}'.");
}

static bool TryBuildAbsoluteUri(string? value, out Uri uri)
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
