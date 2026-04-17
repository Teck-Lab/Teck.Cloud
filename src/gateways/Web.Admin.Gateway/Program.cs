using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Common;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.Options;
using Web.Admin.Gateway.Services;

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
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, Web.Admin.Gateway.Services.MockBearerAuthenticationHandler>("Bearer", _ =>
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

// All routes on the admin gateway require the platform-admin realm role.
// This is enforced here globally so individual routes need no per-route policy.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("PlatformAdmin", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("platform-admin"));

AdminGatewayOptions adminOptions = builder.Configuration.GetAdminGatewayOptions();
builder.Services.AddSingleton(adminOptions);

IConfigurationSection reverseProxyConfiguration = builder.Configuration.GetSection("ReverseProxy");
builder.Services.AddReverseProxy()
    .LoadFromConfig(reverseProxyConfiguration)
    .AddServiceDiscoveryDestinationResolver()
    .AddAdminGatewayTransforms(adminOptions);

builder.Services.AddOpenApi();

WebApplication app = builder.Build();

app.UseRouting();

app.UseBaseInfrastructure();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapOpenApi();
app.MapReverseProxy();

await app.RunAsync();
