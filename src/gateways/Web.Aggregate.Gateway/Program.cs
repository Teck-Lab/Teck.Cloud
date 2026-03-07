using FastEndpoints;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Common;
using SharedKernel.Grpc.Contracts.Remote.V1.ServiceVersions;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.Caching;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Infrastructure.OpenApi;
using SharedKernel.Infrastructure.Options;
using Web.Aggregate.Gateway.Services;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var appOptions = new AppOptions();
builder.Configuration.GetSection(AppOptions.Section).Bind(appOptions);
builder.AddCachingInfrastructure();
builder.AddBaseInfrastructure(appOptions);
builder.Services.AddFastEndpointsInfrastructure(typeof(Program).Assembly);
builder.AddOpenApiInfrastructure(appOptions);

KeycloakAuthenticationOptions keycloakOptions = builder.Configuration.GetKeycloakOptions<KeycloakAuthenticationOptions>()
    ?? throw new InvalidOperationException("Keycloak configuration section is missing or invalid.");

builder.Services.AddKeycloak(builder.Configuration, builder.Environment, keycloakOptions);
builder.Services.AddTeckCloudMultiTenancy();
builder.Services.AddSingleton<IServiceTokenExchangeService, ServiceTokenExchangeService>();
builder.Services.AddScoped<IOutboundSecurityContextFactory, HttpContextOutboundSecurityContextFactory>();

builder.Services.AddHttpClient("KeycloakTokenClient", client =>
{
});

builder.Services.AddHttpClient("CustomerApi", client =>
{
    var customerApiUrl = builder.Configuration["Services:CustomerApi:Url"];
    if (!string.IsNullOrWhiteSpace(customerApiUrl))
    {
        client.BaseAddress = new Uri(customerApiUrl);
    }
}).AddHttpMessageHandler(services => new OutboundTokenExchangeHandler(
    services.GetRequiredService<IHttpContextAccessor>(),
    services.GetRequiredService<IOutboundSecurityContextFactory>(),
    "teck-customer"));

builder.Services.AddHttpClient("CatalogApi", client =>
{
    var catalogApiUrl = builder.Configuration["Services:CatalogApi:Url"];
    if (!string.IsNullOrWhiteSpace(catalogApiUrl))
    {
        client.BaseAddress = new Uri(catalogApiUrl);
    }
}).AddHttpMessageHandler(services => new OutboundTokenExchangeHandler(
    services.GetRequiredService<IHttpContextAccessor>(),
    services.GetRequiredService<IOutboundSecurityContextFactory>(),
    "teck-catalog"));

string[] configuredCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AggregateCors", policy =>
    {
        if (configuredCorsOrigins.Length > 0)
        {
            policy.WithOrigins(configuredCorsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            return;
        }

        if (builder.Environment.IsDevelopment())
        {
            policy
                .SetIsOriginAllowed(origin =>
                {
                    if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri))
                    {
                        return false;
                    }

                    return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
                })
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

var app = builder.Build();

app.MapRemote(
    ResolveRemoteAddress(
        builder.Configuration,
        "Services:CatalogApi:Url"),
    remote =>
    {
        remote.Register<GetCatalogServiceVersionCommand, ServiceVersionRpcResult>();
    });

app.MapRemote(
    ResolveRemoteAddress(
        builder.Configuration,
        "Services:CustomerApi:Url"),
    remote =>
    {
        remote.Register<GetCustomerServiceVersionCommand, ServiceVersionRpcResult>();
    });

app.UseBaseInfrastructure();

app.UseFastEndpointsInfrastructure("aggregate");
app.UseOpenApiInfrastructure(appOptions);

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
